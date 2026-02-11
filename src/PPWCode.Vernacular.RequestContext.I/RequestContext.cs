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

using PPWCode.Util.Authorization.I;
using PPWCode.Util.Time.I;

namespace PPWCode.Vernacular.RequestContext.I;

public abstract class RequestContext<T> : IRequestContext<T>
    where T : struct, IComparable<T>, IEquatable<T>
{
    private readonly IIdentityProvider _identityProvider;

    protected RequestContext(
        ITimeProvider<T> timeProvider,
        IReadOnlyProvider readOnlyProvider,
        IIdentityProvider identityProvider)
    {
        _identityProvider = identityProvider;
        IsReadOnly = readOnlyProvider.IsReadOnly;
        RequestTimestamp = timeProvider.Now;
    }

    /// <inheritdoc />
    public bool IsReadOnly { get; }

    /// <inheritdoc />
    public T RequestTimestamp { get; }

    /// <inheritdoc />
    public string IdentityName
        => _identityProvider.IdentityName;
}
