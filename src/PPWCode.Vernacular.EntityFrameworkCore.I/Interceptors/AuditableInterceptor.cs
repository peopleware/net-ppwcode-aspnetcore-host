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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

using PPWCode.Vernacular.Exceptions.V;
using PPWCode.Vernacular.Persistence.V;
using PPWCode.Vernacular.RequestContext.I;

namespace PPWCode.Vernacular.EntityFrameworkCore.I.Interceptors;

public abstract class AuditableInterceptor<TTimestamp> : SaveChangesInterceptor
    where TTimestamp : struct, IComparable<TTimestamp>, IEquatable<TTimestamp>
{
    private readonly IRequestContext<TTimestamp> _requestContext;

    protected AuditableInterceptor(IRequestContext<TTimestamp> requestContext)
    {
        _requestContext = requestContext;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            UpdateAuditableEntities(eventData.Context);
        }
        else
        {
            throw new ProgrammingError("Expected context to be initialized.");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    protected virtual void UpdateAuditableEntities(DbContext context)
    {
        object requestTimestamp = _requestContext.RequestTimestamp;
        object identityName = _requestContext.IdentityName;

        foreach (EntityEntry<IInsertAuditable<TTimestamp>> entityEntry in
                 context
                     .ChangeTracker
                     .Entries<IInsertAuditable<TTimestamp>>()
                     .Where(e => e.State == EntityState.Added))
        {
            entityEntry.Property(nameof(IInsertAuditable<TTimestamp>.CreatedAt)).CurrentValue = requestTimestamp;
            entityEntry.Property(nameof(IInsertAuditable<TTimestamp>.CreatedBy)).CurrentValue = identityName;
        }

        foreach (EntityEntry<IUpdateAuditable<TTimestamp>> entityEntry in
                 context
                     .ChangeTracker
                     .Entries<IUpdateAuditable<TTimestamp>>()
                     .Where(e => e.State == EntityState.Modified))
        {
            entityEntry.Property(nameof(IUpdateAuditable<TTimestamp>.LastModifiedAt)).CurrentValue = requestTimestamp;
            entityEntry.Property(nameof(IUpdateAuditable<TTimestamp>.LastModifiedBy)).CurrentValue = identityName;
        }
    }
}
