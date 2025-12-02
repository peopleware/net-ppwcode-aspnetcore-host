// Copyright 2024 by PeopleWare n.v..
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Diagnostics.CodeAnalysis;

using PPWCode.Vernacular.Exceptions.V;
using PPWCode.Vernacular.Semantics.V;

namespace PPWCode.Util.Time.I;

public abstract class Period<T>
    : CivilizedObject,
      IPeriod<T>,
      IIsEmpty,
      IEquatable<Period<T>>
    where T : struct, IComparable<T>, IEquatable<T>
{
    // ReSharper disable once UnusedMember.Global
    protected Period()
    {
    }

    [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor", Justification = "Reviewed. Suppression is OK here.")]
    protected Period(T? from, T? to)
    {
        From = from;
        To = to;
    }

    protected abstract T MinValue { get; }

    protected abstract T MaxValue { get; }

    public virtual bool Equals(Period<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return
            (GetType() == other.GetType())
            && Nullable.Equals(From, other.From)
            && Nullable.Equals(To, other.To);
    }

    /// <inheritdoc cref="IIsEmpty.IsEmpty" />
    public bool IsEmpty
        => From is null && To is null;

    public virtual T CoalesceFrom
        => From ?? MinValue;

    public virtual T CoalesceTo
        => To ?? MaxValue;

    public virtual T? From { get; init; }

    public virtual T? To { get; init; }

    /// <inheritdoc />
    public virtual bool Contains(T other)
    {
        if (!IsCivilized)
        {
            throw new ProgrammingError("Validation of period contains can only be done on Civilized objects");
        }

        return (CoalesceFrom.CompareTo(other) <= 0) && (other.CompareTo(CoalesceTo) < 0);
    }

    /// <inheritdoc />
    public virtual bool Contains(IPeriod<T> other)
    {
        if (!IsCivilized || !other.IsCivilized)
        {
            throw new ProgrammingError("Validation of period contains can only be done on Civilized objects");
        }

        return (CoalesceFrom.CompareTo(other.CoalesceFrom) <= 0) && (other.CoalesceTo.CompareTo(CoalesceTo) <= 0);
    }

    /// <inheritdoc />
    public virtual bool Overlaps(IPeriod<T> other)
    {
        if (!IsCivilized || !other.IsCivilized)
        {
            throw new ProgrammingError("Validation of period overlap can only be done on Civilized objects");
        }

        return (CoalesceFrom.CompareTo(other.CoalesceTo) < 0) && (other.CoalesceFrom.CompareTo(CoalesceTo) < 0);
    }

    /// <inheritdoc />
    public virtual bool IsCompletelyContainedWithin(IPeriod<T> other)
    {
        if (!IsCivilized || !other.IsCivilized)
        {
            throw new ProgrammingError("Validation of period contains can only be done on Civilized objects");
        }

        return (other.CoalesceFrom.CompareTo(CoalesceFrom) <= 0)
               && (CoalesceTo.CompareTo(other.CoalesceTo) <= 0);
    }

    /// <inheritdoc />
    public virtual IPeriod<T> OverlappingPeriod(IPeriod<T> other)
    {
        if (!IsCivilized || !other.IsCivilized)
        {
            throw new ProgrammingError("Validation of period overlap can only be done on Civilized objects");
        }

        T? maxFrom = CoalesceFrom.CompareTo(other.CoalesceFrom) > 0 ? From : other.From;
        T? minTo = CoalesceTo.CompareTo(other.CoalesceTo) < 0 ? To : other.To;
        return Create(maxFrom, minTo);
    }

    /// <inheritdoc />
    public T[] PointsInTime
        => [CoalesceFrom, CoalesceTo];

    /// <inheritdoc />
    public override CompoundSemanticException WildExceptions()
    {
        if (IsEmpty)
        {
            return new CompoundSemanticException();
        }

        CompoundSemanticException cse = base.WildExceptions();

        if (CoalesceFrom.CompareTo(CoalesceTo) >= 0)
        {
            cse.AddElement(CreateInvalidExceptionFor(From, To));
        }

        return cse;
    }

    protected abstract SemanticException CreateInvalidExceptionFor(T? from, T? to);

    protected abstract IPeriod<T> Create(T? from, T? to);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as Period<T>);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(From, To);

    public static bool operator ==(Period<T>? left, Period<T>? right)
        => Equals(left, right);

    public static bool operator !=(Period<T>? left, Period<T>? right)
        => !Equals(left, right);

    /// <inheritdoc />
    public override string ToString()
    {
        string? from = From == null ? "-∞" : From.Value.ToString();
        string? to = To == null ? "+∞" : To.Value.ToString();
        return $"[{from}, {to}[";
    }
}
