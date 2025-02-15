namespace Be.Vlaanderen.Basisregisters.ProjectionHandling.Runner.Microsoft
{
    using System;
    using System.IO;
    using global::Microsoft.EntityFrameworkCore;
    using global::Microsoft.EntityFrameworkCore.Design;
    using global::Microsoft.EntityFrameworkCore.Infrastructure;
    using global::Microsoft.Extensions.Configuration;
    using global::Microsoft.Extensions.Logging;
    using MigrationExtensions;

    public interface IRunnerDbContextMigratorFactory
    {
        IRunnerDbContextMigrator CreateMigrator(IConfiguration configuration, ILoggerFactory loggerFactory);
    }

    public abstract class RunnerDbContextMigrationFactory<TContext> : IDesignTimeDbContextFactory<TContext>, IRunnerDbContextMigratorFactory
        where TContext : RunnerDbContext<TContext>
    {
        protected RunnerDbContextMigrationFactory(
            string connectionStringName,
            MigrationHistoryConfiguration migrationHistoryConfiguration)
        {
            if (string.IsNullOrWhiteSpace(connectionStringName))
                throw new ArgumentNullException(nameof(connectionStringName));

            _connectionStringName = connectionStringName;

            _migrationHistoryConfiguration = migrationHistoryConfiguration ?? throw new ArgumentNullException(nameof(migrationHistoryConfiguration));
        }

        private readonly string _connectionStringName;
        private readonly MigrationHistoryConfiguration _migrationHistoryConfiguration;
        
        protected abstract TContext CreateContext(DbContextOptions<TContext> migrationContextOptions);
        protected virtual void ConfigureOptionsBuilder(DbContextOptionsBuilder<TContext> optionsBuilder) { }
        protected virtual void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder sqlServerOptions) { }

        public TContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{Environment.MachineName.ToLowerInvariant()}.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var contextOptions = CreateOptionsBuilder(configuration).Options;

            return CreateContext(contextOptions);
        }

        public IRunnerDbContextMigrator CreateMigrator(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            var contextOptions = CreateOptionsBuilder(configuration, loggerFactory).Options;

            return new RunnerDbContextMigrator<TContext>(() => CreateContext(contextOptions), loggerFactory);
        }

        private DbContextOptionsBuilder<TContext> CreateOptionsBuilder(IConfiguration configuration)
        {
            var connectionString = configuration?.GetConnectionString(_connectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException($"Could not find a connection string with name '{connectionString}'");
            
            var optionsBuilder = new DbContextOptionsBuilder<TContext>()
                .UseSqlServer(
                    connectionString,
                    sqlServerOptions =>
                    {
                        sqlServerOptions.EnableRetryOnFailure();

                        sqlServerOptions.MigrationsHistoryTable(
                            _migrationHistoryConfiguration.Table,
                            _migrationHistoryConfiguration.Schema);

                        ConfigureSqlServerOptions(sqlServerOptions);
                    }
                )
                .UseExtendedSqlServerMigrations();

            ConfigureOptionsBuilder(optionsBuilder);

            return optionsBuilder;
        }        

        private DbContextOptionsBuilder<TContext> CreateOptionsBuilder(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
                throw new ArgumentNullException(nameof(loggerFactory));

            return CreateOptionsBuilder(configuration)
                .UseLoggerFactory(loggerFactory);
        }

        public class MigrationHistoryConfiguration
        {
            public string Schema { get; set; }
            public string Table { get; set; }
        }
    }
}
