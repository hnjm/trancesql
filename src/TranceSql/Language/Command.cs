﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TranceSql.Processing;

namespace TranceSql.Language
{
    public class Command : IEnumerable<ISqlStatement>
    {
        private List<ISqlStatement> _statements = new List<ISqlStatement>();
        private SqlCommandManager _manager;
        private DeferContext _deferContext;

        /// <summary>
        /// Gets the dialect this command is configured for.
        /// </summary>
        internal IDialect Dialect { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="connection">The connection to use when rendering and executing the command.</param>
        public Command(Database connection)
        {
            _manager = connection.Manager;
            Dialect = connection.Dialect;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Command" /> class for executing deferred commands.
        /// If deferred execution is not being used, the <see cref="Command(Database connection)"/> constructor
        /// is a better choice.
        /// </summary>
        /// <param name="connection">The connection to use when rendering and executing the command.</param>
        /// <param name="deferContext">The defer context for this command.</param>
        public Command(Database connection, DeferContext deferContext)
            : this(connection)
        {
            _deferContext = deferContext;
        }

        /// <summary>
        /// Adds the specified statement to this command.
        /// </summary>
        /// <param name="statement">The statement to add.</param>
        public void Add(ISqlStatement statement) => _statements.Add(statement);

        #region Execution

        #region Cached


        /// <summary>
        /// Creates a delegate from current command and returns the result as 
        /// an enumerable list.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <returns>Result of command as a list.</returns>
        public Func<Task<IEnumerable<TResult>>> FetchListCached<TResult>()
        {
            var cached = new CachedContext(Render());
            return () => _manager.ExecuteListResultAsync<TResult>(cached);
        }

        /// <summary>
        /// Creates a delegate from current command to return the result as an enumerable list.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <typeparam name="TParameter">The type of the parameter.</typeparam>
        /// <param name="parameter">The parameter.</param>
        /// <returns>Result of command as a list.</returns>
        public Func<TParameter, Task<IEnumerable<TResult>>> FetchListCached<TResult, TParameter>(Parameter parameter)
        {
            var cached = new CachedContext(Render());
            return p => _manager.ExecuteListResultAsync<TResult>(cached.WithParameters(new Dictionary<string, object> { { parameter.Name, p } }));
        }


        /// <summary>
        /// Creates a delegate from current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="defaultValue">Value to return if result is null or command returns no values.</param>
        /// <returns>Result of command.</returns>
        public Func<Task<TResult>> FetchCached<TResult>(TResult defaultValue = default(TResult))
        {
            var cached = new CachedContext(Render());
            return () => _manager.ExecuteResultAsync<TResult>(Render(), defaultValue, null);
        }

        /// <summary>
        /// Creates a delegate from current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="collections">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Func<Task<TResult>> FetchCached<TResult>(params Expression<Func<TResult, IEnumerable>>[] collections)
        {
            var cached = new CachedContext(Render());
            return () => _manager.ExecuteResultAsync<TResult>(Render(), default(TResult), collections.Select(c => c.GetPropertyInfo()));
        }

        /// <summary>
        /// Creates a delegate from current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="collections">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Func<Task<TResult>> FetchCached<TResult>(IEnumerable<PropertyInfo> collections)
        {
            if (!collections.All(p => p.PropertyType.ImplementsInterface<IEnumerable>()))
            {
                throw new ArgumentException("All properties must be collections", "collections");
            }

            var cached = new CachedContext(Render());
            return () => _manager.ExecuteResultAsync<TResult>(Render(), default(TResult), collections);
        }

        /// <summary>
        /// Creates a delegate from current command and maps multiple commands to a single result class. Use
        /// this method to populate a result with multiple commands.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="map">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Func<Task<TResult>> FetchMappedResultCached<TResult>(params Expression<Func<TResult, object>>[] map)
            where TResult : new()
        {
            var mappedProperties = MapProperties<TResult>(map);
            var cached = new CachedContext(Render());
            return () => _manager.ExecuteMapResultAsync<TResult>(Render(), mappedProperties);
        }
        
        /// <summary>
        /// Creates a delegate from current command and maps multiple commands to a single result class. Use
        /// this method to populate a result with multiple commands.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="map">A list of properties that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Func<Task<TResult>> FetchMappedResultCached<TResult>(IEnumerable<PropertyInfo> map)
            where TResult : new()
        {
            var cached = new CachedContext(Render());
            return () => _manager.ExecuteMapResultAsync<TResult>(Render(), MapProperties(map));
        }

        /// <summary>
        /// Creates a delegate from current command and performs a custom action
        /// to create the result type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="valueProvider">
        /// Delegate function to convert the result to the specified type.
        /// </param>
        /// <returns>Result of command.</returns>
        public Func<Task<TResult>> FetchCustomResultCached<TResult>(CreateEntity<TResult> valueProvider)
        {
            var cached = new CachedContext(Render());
            return () => _manager.ExecuteCustomAsync<TResult>(Render(), valueProvider);
        }

        /// <summary>
        /// Creates a delegate from current command and returns the first two columns of the result as
        /// a dictionary.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <returns>
        /// Result of command as a dictionary.
        /// </returns>
        public Func<Task<IDictionary<TKey, TValue>>> FetchRowKeyedDictionaryCached<TKey, TValue>()
        {
            var cached = new CachedContext(Render());
            return () => _manager.ExecuteRowKeyedDictionaryResultAsync<TKey, TValue>(Render());
        }

        /// <summary>
        /// Fetches the first row of command as a dictionary with the column names as keys
        /// and the result row values as values.
        /// </summary>
        /// <param name="columns">The columns to return. If null, all columns will be returned.</param>
        /// <returns>Result of command as a dictionary.</returns>
        public Func<Task<IDictionary<string, object>>> FetchColumnKeyedDictionaryCached(params string[] columns)
        {
            var cached = new CachedContext(Render());
            return () => _manager.ExecuteColumnKeyedDictionaryResultAsync(Render(), columns);
        }

        /// <summary>
        /// Creates a delegate from current command and returns a count of the
        /// number of rows affected.
        /// </summary>
        /// <returns>The number of rows affected by the command.</returns>
        public Func<Task<int>> ExecuteCached()
        {
            var cached = new CachedContext(Render());
            return () => _manager.ExecuteAsync(Render());
        }
        
        #endregion

        #region Synchronous

        /// <summary>
        /// Executes the current command and returns the result as 
        /// an enumerable list.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <returns>Result of command as a list.</returns>
        public IEnumerable<TResult> FetchList<TResult>()
        {
            return _manager.ExecuteListResult<TResult>(Render());
        }

        /// <summary>
        /// Executes the current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="defaultValue">Value to return if result is null or command returns no values.</param>
        /// <returns>Result of command.</returns>
        public TResult Fetch<TResult>(TResult defaultValue = default(TResult))
        {
            return _manager.ExecuteResult<TResult>(Render(), defaultValue, null);
        }

        /// <summary>
        /// Executes the current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="collections">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public TResult Fetch<TResult>(params Expression<Func<TResult, IEnumerable>>[] collections)
        {
            return _manager.ExecuteResult<TResult>(Render(), default(TResult), collections.Select(c => c.GetPropertyInfo()));
        }

        /// <summary>
        /// Executes the current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="collections">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public TResult Fetch<TResult>(IEnumerable<PropertyInfo> collections)
        {
            if (!collections.All(p => p.PropertyType.ImplementsInterface<IEnumerable>()))
                throw new ArgumentException("All properties must be collections", "collections");

            return _manager.ExecuteResult<TResult>(Render(), default(TResult), collections);
        }

        /// <summary>
        /// Executes the current command and maps multiple commands to a single result class. Use
        /// this method to populate a result with multiple commands.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="map">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public TResult FetchMappedResult<TResult>(params Expression<Func<TResult, object>>[] map)
            where TResult : new()
        {
            var mappedProperties = MapProperties<TResult>(map);
            return _manager.ExecuteMapResult<TResult>(Render(), mappedProperties);
        }

        /// <summary>
        /// Executes the current command and maps multiple commands to a single result class. Use
        /// this method to populate a result with multiple commands.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="map">A list of properties that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public TResult FetchMappedResult<TResult>(IEnumerable<PropertyInfo> map)
            where TResult : new()
        {
            return _manager.ExecuteMapResult<TResult>(Render(), MapProperties(map));
        }

        /// <summary>
        /// Executes the current command and performs a custom action
        /// to create the result type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="valueProvider">
        /// Delegate function to convert the result to the specified type.
        /// </param>
        /// <returns>Result of command.</returns>
        public TResult FetchCustomResult<TResult>(CreateEntity<TResult> valueProvider)
        {
            return _manager.ExecuteCustom<TResult>(Render(), valueProvider);
        }

        /// <summary>
        /// Executes the current command and returns the first two columns of the result as
        /// a dictionary.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <returns>
        /// Result of command as a dictionary.
        /// </returns>
        public IDictionary<TKey, TValue> FetchRowKeyedDictionary<TKey, TValue>()
        {
            return _manager.ExecuteRowKeyedDictionaryResult<TKey, TValue>(Render());
        }

        /// <summary>
        /// Fetches the first row of command as a dictionary with the column names as keys
        /// and the result row values as values.
        /// </summary>
        /// <param name="columns">The columns to return. If null, all columns will be returned.</param>
        /// <returns>Result of command as a dictionary.</returns>
        public IDictionary<string, object> FetchColumnKeyedDictionary(params string[] columns)
        {
            return _manager.ExecuteColumnKeyedDictionaryResult(Render(), columns);
        }

        /// <summary>
        /// Executes the current command and returns a count of the
        /// number of rows affected.
        /// </summary>
        /// <returns>The number of rows affected by the command.</returns>
        public int Execute()
        {
            return _manager.Execute(Render());
        }

        #endregion

        #region Async

        /// <summary>
        /// Executes the current command and returns the result as 
        /// an enumerable list.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <returns>Result of command as a list.</returns>
        public Task<IEnumerable<TResult>> FetchListAsync<TResult>()
        {
            return _manager.ExecuteListResultAsync<TResult>(Render());
        }

        /// <summary>
        /// Executes the current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="defaultValue">Value to return if result is null or command returns no values.</param>
        /// <returns>Result of command.</returns>
        public Task<TResult> FetchAsync<TResult>(TResult defaultValue = default(TResult))
        {
            return _manager.ExecuteResultAsync<TResult>(Render(), defaultValue, null);
        }

        /// <summary>
        /// Executes the current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="collections">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Task<TResult> FetchAsync<TResult>(params Expression<Func<TResult, IEnumerable>>[] collections)
        {
            return _manager.ExecuteResultAsync<TResult>(Render(), default(TResult), collections.Select(c => c.GetPropertyInfo()));
        }

        /// <summary>
        /// Executes the current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="collections">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Task<TResult> FetchAsync<TResult>(IEnumerable<PropertyInfo> collections)
        {
            if (!collections.All(p => p.PropertyType.ImplementsInterface<IEnumerable>()))
            {
                throw new ArgumentException("All properties must be collections", "collections");
            }

            return _manager.ExecuteResultAsync<TResult>(Render(), default(TResult), collections);
        }

        /// <summary>
        /// Executes the current command and maps multiple commands to a single result class. Use
        /// this method to populate a result with multiple commands.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="map">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Task<TResult> FetchMappedResultAsync<TResult>(params Expression<Func<TResult, object>>[] map)
            where TResult : new()
        {
            var mappedProperties = MapProperties<TResult>(map);
            return _manager.ExecuteMapResultAsync<TResult>(Render(), mappedProperties);
        }

        /// <summary>
        /// Maps a list of properties expression to properties and types.
        /// </summary>
        /// <typeparam name="TResult">The entity type to select.</typeparam>
        /// <param name="map">A list of property select expressions.</param>
        /// <returns>A list of properties and their types.</returns>
        private static IEnumerable<Tuple<PropertyInfo, Type>> MapProperties<TResult>(IEnumerable<Expression<Func<TResult, object>>> map)
        {
            return MapProperties(map.Select(c => c.GetPropertyInfo()));
        }

        /// <summary>
        /// Maps a list of properties to their property types.
        /// </summary>
        /// <param name="properties">The properties to map.</param>
        /// <returns>A list of properties and their types.</returns>
        private static IEnumerable<Tuple<PropertyInfo, Type>> MapProperties(IEnumerable<PropertyInfo> properties)
        {
            return properties.Select(p => new Tuple<PropertyInfo, Type>(p, p.PropertyType.GetCollectionType() ?? p.PropertyType));
        }

        /// <summary>
        /// Executes the current command and maps multiple commands to a single result class. Use
        /// this method to populate a result with multiple commands.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="map">A list of properties that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Task<TResult> FetchMappedResultAsync<TResult>(IEnumerable<PropertyInfo> map)
            where TResult : new()
        {
            return _manager.ExecuteMapResultAsync<TResult>(Render(), MapProperties(map));
        }

        /// <summary>
        /// Executes the current command and performs a custom action
        /// to create the result type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="valueProvider">
        /// Delegate function to convert the result to the specified type.
        /// </param>
        /// <returns>Result of command.</returns>
        public Task<TResult> FetchCustomResultAsync<TResult>(CreateEntity<TResult> valueProvider)
        {
            return _manager.ExecuteCustomAsync<TResult>(Render(), valueProvider);
        }

        /// <summary>
        /// Executes the current command and returns the first two columns of the result as
        /// a dictionary.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <returns>
        /// Result of command as a dictionary.
        /// </returns>
        public Task<IDictionary<TKey, TValue>> FetchRowKeyedDictionaryAsync<TKey, TValue>()
        {
            return _manager.ExecuteRowKeyedDictionaryResultAsync<TKey, TValue>(Render());
        }

        /// <summary>
        /// Fetches the first row of command as a dictionary with the column names as keys
        /// and the result row values as values.
        /// </summary>
        /// <param name="columns">The columns to return. If null, all columns will be returned.</param>
        /// <returns>Result of command as a dictionary.</returns>
        public Task<IDictionary<string, object>> FetchColumnKeyedDictionaryAsync(params string[] columns)
        {
            return _manager.ExecuteColumnKeyedDictionaryResultAsync(Render(), columns);
        }

        /// <summary>
        /// Executes the current command and returns a count of the
        /// number of rows affected.
        /// </summary>
        /// <returns>The number of rows affected by the command.</returns>
        public Task<int> ExecuteAsync()
        {
            return _manager.ExecuteAsync(Render());
        }

        #endregion

        #region Deferred

        /// <summary>
        /// Executes the current command and returns the result as 
        /// an enumerable list.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <returns>Result of command as a list.</returns>
        public Deferred<IEnumerable<TResult>> FetchListDeferred<TResult>()
        {
            AssertDeferredAvailable();
            return _deferContext.ExecuteListResultDeferred<TResult>(Render());
        }

        /// <summary>
        /// Executes the current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="defaultValue">Value to return if result is null or command returns no values.</param>
        /// <returns>Result of command.</returns>
        public Deferred<TResult> FetchDeferred<TResult>(TResult defaultValue = default(TResult))
        {
            AssertDeferredAvailable();
            return _deferContext.ExecuteResultDeferred<TResult>(Render(), defaultValue, null);
        }

        /// <summary>
        /// Executes the current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="collections">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Deferred<TResult> FetchDeferred<TResult>(params Expression<Func<TResult, object>>[] collections)
        {
            AssertDeferredAvailable();
            return _deferContext.ExecuteResultDeferred<TResult>(Render(), default(TResult), collections.Select(c => c.GetPropertyInfo()));
        }

        /// <summary>
        /// Executes the current command and returns a single row as
        /// the specified type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="collections">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Deferred<TResult> FetchDeferred<TResult>(IEnumerable<PropertyInfo> collections)
        {
            if (!collections.All(p => p.PropertyType.ImplementsInterface<IEnumerable>()))
                throw new ArgumentException("All properties must be collections", "collections");

            AssertDeferredAvailable();

            return _deferContext.ExecuteResultDeferred<TResult>(Render(), default(TResult), collections);
        }

        /// <summary>
        /// Executes the current command and maps multiple commands to a single result class. Use
        /// this method to populate a result with multiple commands.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="map">A list of IEnumerable property selectors that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Deferred<TResult> FetchMappedResultDeferred<TResult>(params Expression<Func<TResult, object>>[] map)
            where TResult : new()
        {
            AssertDeferredAvailable();
            var mappedProperties = MapProperties<TResult>(map);
            return _deferContext.ExecuteMapResultDeferred<TResult>(Render(), mappedProperties);
        }

        /// <summary>
        /// Executes the current command and maps multiple commands to a single result class. Use
        /// this method to populate a result with multiple commands.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="map">A list of properties that should be populated from the command.
        /// These properties should appear in the same order as their select command.</param>
        /// <returns>The result of the SQL command.</returns>
        /// <returns>
        /// Result of command.
        /// </returns>
        public Deferred<TResult> FetchMappedResultDeferred<TResult>(IEnumerable<PropertyInfo> map)
            where TResult : new()
        {
            AssertDeferredAvailable();
            return _deferContext.ExecuteMapResultDeferred<TResult>(Render(), MapProperties(map));
        }

        /// <summary>
        /// Executes the current command and performs a custom action
        /// to create the result type.
        /// </summary>
        /// <typeparam name="TResult">Result item type</typeparam>
        /// <param name="valueProvider">
        /// Delegate function to convert the result to the specified type.
        /// </param>
        /// <returns>Result of command.</returns>
        public Deferred<TResult> FetchCustomResultDeferred<TResult>(CreateEntity<TResult> valueProvider)
        {
            AssertDeferredAvailable();
            return _deferContext.ExecuteCustomDeferred<TResult>(Render(), valueProvider);
        }

        /// <summary>
        /// Executes the current command and returns the first two columns of the result as
        /// a dictionary.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <returns>
        /// Result of command as a dictionary.
        /// </returns>
        public Deferred<IDictionary<TKey, TValue>> FetchRowKeyedDictionaryDeferred<TKey, TValue>()
        {
            AssertDeferredAvailable();
            return _deferContext.ExecuteRowKeyedDictionaryResultDeferred<TKey, TValue>(Render());
        }

        /// <summary>
        /// Fetches the first row of command as a dictionary with the column names as keys
        /// and the result row values as values.
        /// </summary>
        /// <param name="columns">The columns to return. If null, all columns will be returned.</param>
        /// <returns>Result of command as a dictionary.</returns>
        public Deferred<IDictionary<string, object>> FetchColumnKeyedDictionaryDeferred(params string[] columns)
        {
            AssertDeferredAvailable();
            return _deferContext.ExecuteColumnKeyedDictionaryResultDeferred(Render(), columns);
        }

        /// <summary>
        /// Ensures the result of the deferred context's operation is available, otherwise
        /// throws an exception.
        /// </summary>
        private void AssertDeferredAvailable()
        {
            if (_deferContext == null)
                throw new InvalidOperationException("No deferred command context exists for this command. To execute a deferred command you must provide a context when the command instance is created.");
            if (_deferContext.HasExecuted)
                throw new InvalidOperationException("The deferred context for this command has already executed. To execute additional deferred commands you must create a new deferred context.");

        }

        /// <summary>
        /// Executes the current command and returns a count of the
        /// number of rows affected.
        /// </summary>
        /// <returns>The number of rows affected by the command.</returns>
        public void ExecuteDeferred()
        {
            _deferContext.ExecuteDeferred(Render());
        }

        #endregion

        #endregion

        private RenderContext Render()
        {
            var context = new RenderContext(Dialect, _deferContext);
            context.RenderDelimited(_statements, context.LineDelimiter);

            return context;
        }

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        IEnumerator<ISqlStatement> IEnumerable<ISqlStatement>.GetEnumerator()
            => _statements.GetEnumerator();

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"></see> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator() => _statements.GetEnumerator();

        #endregion
    }
}
