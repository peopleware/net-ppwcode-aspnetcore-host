// Copyright 2025 by PeopleWare n.v..
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using PPWCode.Util.Time.I;
using PPWCode.Vernacular.Contracts.I;
using PPWCode.Vernacular.Persistence.V;
using PPWCode.Vernacular.RequestContext.I;
using PPWCode.Vernacular.Semantics.V;

using SemanticException = PPWCode.Vernacular.Exceptions.V.SemanticException;

namespace PPWCode.Vernacular.HistoryEvent.I;

/// <inheritdoc />
public abstract class HistoryEventStore<TOwner, TEvent, TId, TKnowledgePeriod, TKnowledge, TContext>
    : IHistoryEventStore<TOwner, TEvent, TId, TKnowledgePeriod, TKnowledge, TContext, TEvent>
    where TOwner : notnull
    where TEvent : IHistoryEvent<TKnowledgePeriod, TKnowledge, TOwner, TEvent>, IPersistentObject<TId>
    where TId : IEquatable<TId>
    where TKnowledgePeriod : Period<TKnowledge>, new()
    where TKnowledge : struct, IComparable<TKnowledge>, IEquatable<TKnowledge>
    where TContext : IHistoryEventStoreContext
{
    private readonly IRequestContext<TKnowledge> _requestContext;
    private readonly IDictionary<TOwner, ISet<TEvent>> _ownerEvents = new Dictionary<TOwner, ISet<TEvent>>();
    private readonly ISet<TOwner> _touchedOwners = new HashSet<TOwner>();

    private TKnowledge? _currentTransactionTime;
    private TKnowledge? _previousTransactionTime;

    protected HistoryEventStore(IRequestContext<TKnowledge> requestContext)
    {
        _requestContext = requestContext;
    }

    /// <inheritdoc />
    public virtual TEvent Open(TEvent @event, TKnowledge transactionTime, TContext? context = default)
    {
        // when using the same event-store to process multiple transaction-times
        // each transaction-time should be the same or more recent than the previous one
        Contract.Requires(transactionTime.CompareTo(_requestContext.RequestTimestamp) <= 0);
        Contract.Requires(_previousTransactionTime is null || (_previousTransactionTime.Value.CompareTo(transactionTime) <= 0));
        Contract.Requires(_currentTransactionTime is null || _currentTransactionTime.Value.Equals(transactionTime));

        _currentTransactionTime ??= transactionTime;
        @event.KnowledgePeriod = new TKnowledgePeriod { From = transactionTime, To = null };
        StoreEvent(@event);

        return @event;
    }

    /// <inheritdoc />
    public virtual TEvent Close(TEvent @event, TKnowledge transactionTime, TContext? context = default)
    {
        // when using the same event-store to process multiple transaction-times
        // each transaction-time should be the same or more recent than the previous one
        Contract.Requires(transactionTime.CompareTo(_requestContext.RequestTimestamp) <= 0);
        Contract.Requires(_previousTransactionTime is null || (_previousTransactionTime.Value.CompareTo(transactionTime) <= 0));
        Contract.Requires(_currentTransactionTime is null || _currentTransactionTime.Value.Equals(transactionTime));
        Contract.Requires(@event.KnowledgePeriod is not null);

        _currentTransactionTime ??= transactionTime;
        @event.KnowledgePeriod = new TKnowledgePeriod { From = @event.KnowledgePeriod!.From, To = transactionTime };

        StoreEvent(@event);

        return @event;
    }

    public virtual void UndoClose(TEvent originalEvent, TContext? context = default)
    {
        Contract.Assert(originalEvent.KnowledgePeriod is not null);

        originalEvent.KnowledgePeriod = new TKnowledgePeriod { From = originalEvent.KnowledgePeriod.From, To = null };
    }

    /// <inheritdoc />
    public virtual Task<ISet<TEvent>> ProcessAsync(TContext? context = default, Func<TEvent, TContext?, CancellationToken, Task>? onCreate = null, CancellationToken cancellationToken = default)
        => ProcessAsync(_requestContext.RequestTimestamp, context, onCreate, cancellationToken);

    /// <inheritdoc />
    public virtual async Task<ISet<TEvent>> ProcessAsync(TKnowledge transactionTime, TContext? context = default, Func<TEvent, TContext?, CancellationToken, Task>? onCreate = null, CancellationToken cancellationToken = default)
    {
        // when using the same event-store to process multiple transaction-times
        // each transaction-time should be the same or more recent than the previous one
        Contract.Requires(transactionTime.CompareTo(_requestContext.RequestTimestamp) <= 0);
        Contract.Requires(_previousTransactionTime is null || (_previousTransactionTime.Value.CompareTo(transactionTime) <= 0));
        Contract.Requires(_currentTransactionTime is null || _currentTransactionTime.Value.Equals(transactionTime));

        _currentTransactionTime ??= transactionTime;

        ISet<TEvent> result = new HashSet<TEvent>();

        // only visit touched roots
        foreach (TOwner owner in _touchedOwners)
        {
            // 1. Only civilized owners can be processed
            // 2. there must be events for this root
            // 3. if there is an empty-knowledge-period, it can only be on the current transaction-time
            Contract.Assert(owner is ICivilizedObject civilizedObject ? civilizedObject.IsCivilized : true);
            Contract.Assert(_ownerEvents.ContainsKey(owner));
            Contract.Assert(
                _ownerEvents[owner]
                    .All(e => !e.KnowledgePeriod!.From.Equals(e.KnowledgePeriod.To) || e.KnowledgePeriod.From.Equals(transactionTime)));

            // events-to-process should only contain events from the current transaction-time
            // the previous transaction-time could be the same as the current one,
            // but any events that are not on the current transaction-time must be older and can be ignored
            IEnumerable<TEvent> eventsToIgnore =
                _ownerEvents[owner]
                    .Where(e => !e.KnowledgePeriod!.From.Equals(transactionTime) && !e.KnowledgePeriod.To.Equals(transactionTime));
            _ownerEvents[owner].ExceptWith(eventsToIgnore);
            ISet<TEvent> events = _ownerEvents[owner];

            // remove events with an empty knowledge period [X, X[
            TEvent[] emptyKnowledgePeriods =
                events
                    .Where(e => e.KnowledgePeriod!.From.Equals(e.KnowledgePeriod.To))
                    .ToArray();
            foreach (TEvent @event in emptyKnowledgePeriods)
            {
                if (!IsTransient(@event, context))
                {
                    await DeleteAsync(@event, context, cancellationToken).ConfigureAwait(false);
                }

                events.Remove(@event);
            }

            // optimize identical events with consecutive knowledge periods
            ISet<TEvent> eventsToProcess = new HashSet<TEvent>(events);
            while (eventsToProcess.Count > 0)
            {
                // generate candidates with same properties and execution-period
                TEvent @event = eventsToProcess.First();
                TEvent[] candidates =
                    eventsToProcess
                        .Where(e => @event.HasIdenticalEventProperties(e))
                        .Where(e => HasIdenticalExecutionPeriod(e, @event))
                        .OrderBy(e => e.KnowledgePeriod!.From)
                        .ToArray();
                Contract.Assert(candidates.Length <= 2);

                // merge if consecutive and next if possible
                if (candidates.Length == 2)
                {
                    TEvent original = candidates[0];
                    TEvent candidate = candidates[1];

                    // if there are 2 candidates, the following should always be true,
                    // this must be true, since only the events related to transaction-time are processed
                    Contract.Assert(original.KnowledgePeriod is not null);
                    Contract.Assert(candidate.KnowledgePeriod is not null);
                    Contract.Assert(original.KnowledgePeriod.To.Equals(candidate.KnowledgePeriod.From));
                    Contract.Assert(candidate.KnowledgePeriod.From.Equals(transactionTime));
                    Contract.Assert(candidate.KnowledgePeriod.To == null);

                    UndoClose(original, context);

                    if (!IsTransient(candidate, context))
                    {
                        await DeleteAsync(candidate, context, cancellationToken).ConfigureAwait(false);
                    }

                    events.Remove(candidate);
                    eventsToProcess.Remove(candidate);
                    eventsToProcess.Remove(original);
                    result.Add(original);
                }
                else
                {
                    Contract.Assert(candidates.Length == 1);
                    Contract.Assert(candidates[0].Equals(@event));

                    eventsToProcess.Remove(@event);
                    result.Add(@event);
                }
            }

            // Give the ability they do extra processing in our root object
            OnProcessRoot(owner, context);
        }

        // save all transient events
        foreach (TEvent @event in result.Where(e => IsTransient(e, context)))
        {
            if (onCreate is not null)
            {
                await onCreate.Invoke(@event, context, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await InsertAsync(@event, context, cancellationToken).ConfigureAwait(false);
            }
        }

        // Allow extra functionality
        OnProcessActualEvents(result, context);

        // reset transaction time
        Contract.Assert(_currentTransactionTime != null);
        _previousTransactionTime = _currentTransactionTime;
        _currentTransactionTime = null;
        _touchedOwners.Clear();
        _ownerEvents.Clear();

        return result;
    }

    /// <inheritdoc />
    public abstract bool IsTransient(TEvent @event, TContext? context = default);

    /// <summary>
    ///     Compares two events of type <typeparamref name="TEvent" />, if they have the same execution period, it should
    ///     return
    ///     true.
    /// </summary>
    /// <param name="event">The event that being used for equality of their executing period</param>
    /// <param name="otherEvent">The other event that being used for equality of their executing period</param>
    /// <returns>
    ///     Returns <c>true</c> if both have the same execution period or none of them implement
    ///     <see cref="IExecutionPeriod{TExecutionPeriod,TExecution}" />
    /// </returns>
    protected abstract bool HasIdenticalExecutionPeriod(TEvent @event, TEvent otherEvent);

    /// <summary>
    ///     Given an event of type <typeparamref name="TEvent" />, extract / determine a root object of type
    ///     <typeparamref name="TOwner" />
    /// </summary>
    /// <param name="event">Object where we extract an object of type <typeparamref name="TOwner" /></param>
    /// <returns>
    ///     An object of type <typeparamref name="TOwner" />
    /// </returns>
    protected virtual TOwner ExtractOwnerFrom(TEvent @event)
        => @event.Owner;

    /// <summary>
    ///     Give the ability to do some specific tasks, when we are processing a
    ///     <typeparam name="TOwner"> object</typeparam>
    /// </summary>
    /// <param name="owner">An  <typeparamref name="TOwner" object /></param>
    /// <param name="context">optional context of type <typeparamref name="TContext" /></param>
    protected abstract void OnProcessRoot(TOwner owner, TContext? context);

    /// <summary>
    ///     Give the ability to do some specific tasks given the <typeparamref name="TEvent" />s
    ///     before these <typeparamref name="TEvent" />s are persisted to the database
    /// </summary>
    /// <param name="events">The <typeparamref name="TEvent" />s to base the tasks on</param>
    /// <param name="context">optional context of type <typeparamref name="TContext" /></param>
    protected abstract void OnProcessActualEvents(ISet<TEvent> events, TContext? context);

    /// <summary>
    ///     Delete the <paramref name="event" /> in the data store.
    /// </summary>
    /// <remarks>The <paramref name="event" /> is guaranteed a non-transient entity</remarks>
    /// <param name="event">event to be deleted</param>
    /// <param name="context">optional context of type <typeparamref name="TContext" /></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     an awaitable <see cref="Task" />
    /// </returns>
    /// /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is canceled.</exception>
    protected abstract Task DeleteAsync(TEvent @event, TContext? context, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Add the <paramref name="event" /> in the data store.
    /// </summary>
    /// <remarks>The <paramref name="event" /> is guaranteed a transient entity</remarks>
    /// <param name="event">event to be added</param>
    /// <param name="context">optional context of type <typeparamref name="TContext" /></param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     an awaitable <see cref="Task" />
    /// </returns>
    /// /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is canceled.</exception>
    protected abstract Task InsertAsync(TEvent @event, TContext? context, CancellationToken cancellationToken = default);

    private void StoreEvent(TEvent @event)
    {
        TOwner? root = ExtractOwnerFrom(@event);
        if (root is null)
        {
            throw new SemanticException($"The @event can not be store without an owner.");
        }

        if (!_ownerEvents.ContainsKey(root))
        {
            _ownerEvents[root] = new HashSet<TEvent>();
        }

        _ownerEvents[root].Add(@event);
        _touchedOwners.Add(root);
    }
}
