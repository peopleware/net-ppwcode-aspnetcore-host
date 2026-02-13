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

using Microsoft.AspNetCore.Http;

using PPWCode.Vernacular.RequestContext.I;

namespace PPWCode.AspNetCore.Host.I.Providers;

public sealed class WebApiReadOnlyProvider : IReadOnlyProvider
{
    private static readonly ISet<string> _safeHttpMethods =
        new HashSet<string>(
            [
                HttpMethod.Head.ToString(),
                HttpMethod.Get.ToString(),
                HttpMethod.Options.ToString(),
                HttpMethod.Trace.ToString()
            ],
            StringComparer.OrdinalIgnoreCase);

    private readonly IHttpContextAccessor _httpContextAccessor;

    public WebApiReadOnlyProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext HttpContext
        => _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext));

    /// <inheritdoc />
    public bool IsReadOnly
        => _safeHttpMethods.Contains(HttpContext.Request.Method);
}
