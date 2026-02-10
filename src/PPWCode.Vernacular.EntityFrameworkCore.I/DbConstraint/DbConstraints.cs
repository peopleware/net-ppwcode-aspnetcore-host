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

using System.Data;
using System.Data.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using PPWCode.Vernacular.Exceptions.V;

namespace PPWCode.Vernacular.EntityFrameworkCore.I.DbConstraint
{
    public abstract class DbConstraints : IDbConstraints
    {
        private static readonly object _locker = new ();

        private static readonly ISet<DbConstraintMetadata> _emptyDbConstraintMetadata =
            new HashSet<DbConstraintMetadata>();

        private readonly IConfiguration _configuration;
        private readonly string? _connectionStringName;

        private readonly ILogger<DbConstraints> _logger;

        private volatile IDictionary<DbConstraintMetadataKey, DbConstraintMetadata>? _constraints;

        protected DbConstraints(
            ILogger<DbConstraints> logger,
            IConfiguration configuration,
            string? connectionStringName = null)
        {
            _logger = logger;
            _configuration = configuration;
            _connectionStringName = connectionStringName;
        }

        protected abstract DbProviderFactory DbProviderFactory { get; }

        public ISet<DbConstraintMetadata> Constraints
            => _constraints != null
                   ? new HashSet<DbConstraintMetadata>(_constraints.Values)
                   : _emptyDbConstraintMetadata;

        public DbConstraintMetadata? GetByConstraintName(string schemaName, string constraintName)
        {
            if (_constraints == null)
            {
                _logger.LogWarning("Consider calling Initialize(), before the host is started");
                Initialize();
            }

            if (_constraints != null)
            {
                DbConstraintMetadataKey key = new (schemaName, constraintName);
                _constraints.TryGetValue(key, out DbConstraintMetadata? constraint);
                return constraint;
            }

            return null;
        }

        public void Initialize()
        {
            if (_constraints == null)
            {
                lock (_locker)
                {
                    if (_constraints == null)
                    {
                        OnInitialize();
                    }
                }
            }
        }

        public void ReInitialize()
        {
            lock (_locker)
            {
                _constraints = null;
                OnInitialize();
            }
        }

        protected abstract DbCommand GetCommand(DbConnection connection, DbTransaction transaction);
        protected abstract DbConstraintMetadata GetDbConstraintMetadata(DbDataReader reader);

        protected virtual void OnInitialize()
        {
            // Determine connection string
            // 1) if we have only one connection string, we will use it
            // 2) Search for a key, named ConnectionStringName, this should be the key inside theConnectionStrings section
            string? connectionStringName = _connectionStringName;
            if (connectionStringName is null)
            {
                IEnumerable<IConfigurationSection> children =
                    _configuration
                        .GetSection("ConnectionStrings")
                        .GetChildren();
                connectionStringName =
                    children.Count() == 1
                        ? children.First().Key
                        : SearchConfigForKey(_configuration, "ConnectionStringName");
            }

            if (connectionStringName is null)
            {
                throw new ProgrammingError("Unable to get a connection string using ADO.Net, no connection string could be determined");
            }

            using DbConnection connection = GetConnection(connectionStringName);
            connection.Open();

            using (DbTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                using (DbCommand command = GetCommand(connection, transaction))
                {
                    _constraints = new Dictionary<DbConstraintMetadataKey, DbConstraintMetadata>();
                    using (DbDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                DbConstraintMetadata metaData = GetDbConstraintMetadata(reader);
                                _constraints.Add(metaData.Key, metaData);
                            }
                        }
                    }
                }

                transaction.Commit();
            }

            connection.Close();
        }

        protected virtual DbConnection GetConnection(string connectionStringName)
        {
            string? connectionString = _configuration.GetConnectionString(connectionStringName);
            if (connectionString is not null)
            {
                DbConnection? dbConnection = DbProviderFactory.CreateConnection();
                if (dbConnection is not null)
                {
                    dbConnection.ConnectionString = connectionString;
                    return dbConnection;
                }
            }

            throw new ProgrammingError("Unable to get a database-connection using ADO.Net");
        }

        protected virtual string? SearchConfigForKey(IConfiguration config, string targetKey, string prefix = "")
        {
            string? result = null;
            foreach (IConfigurationSection section in config.GetChildren())
            {
                string path = string.IsNullOrEmpty(prefix) ? section.Key : $"{prefix}:{section.Key}";
                if (string.Equals(section.Key, targetKey, StringComparison.OrdinalIgnoreCase))
                {
                    return section.Value;
                }

                result = SearchConfigForKey(section, targetKey, path);
                if (result is not null)
                {
                    break;
                }
            }

            return result;
        }
    }
}
