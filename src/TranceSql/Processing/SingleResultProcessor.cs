﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TranceSql.Processing
{
    /// <summary>
    /// Result processor that returns a single row as the specified type. Addition properties
    /// can be populated from additional result sets returned by the query.
    /// </summary>
    internal class SingleResultProcessor<TResult> : IResultProcessor
    {
        private readonly TResult _defaultResult;
        private readonly IEnumerable<PropertyInfo> _properties;
        private static readonly MethodInfo readData = typeof(EntityMappingHelper).GetMethod("ReadData");

        /// <summary>
        /// Result processor that returns a single row as the specified type. Addition properties
        /// can be populated from additional result sets returned by the query.
        /// </summary>
        /// <param name="defaultResult">The default result to return if the result is empty.</param>
        /// <param name="properties">Additional properties to populate from subsequent result sets.</param>
        public SingleResultProcessor(TResult defaultResult, IEnumerable<PropertyInfo> properties)
        {
            _defaultResult = defaultResult;
            _properties = properties;
        }

        /// <summary>
        /// Processes the result as a single entity result.
        /// </summary>
        /// <param name="reader">An open data reader queued to the appropriate result set.</param>
        /// <returns>The result for this query.</returns>
        public object Process(DbDataReader reader)
        {
            if (EntityMapping.IsSimpleType<TResult>())
            {
                // requested type can be mapped directly to a CLR type

                if (reader.Read())
                {
                    return EntityMapping.ReadHelper.Get<TResult>(reader, 0, _defaultResult);
                }
                else
                {
                    return _defaultResult;
                }
            }
            else
            {
                // else if requested type must be mapped from the result row

                var result = reader.CreateInstance<TResult>(_defaultResult);

                // Populate collections
                if (_properties != null)
                {
                    foreach (var collection in _properties)
                    {
                        if (!reader.NextResult())
                        {
                            throw new InvalidOperationException("Not enough result sets were returned by the query to assign all the requested properties.");
                        }

                        // Don't try to assign the result if it is null
                        if (result != null)
                        {
                            var genericReadData = readData.MakeGenericMethod(collection.PropertyType.GetCollectionType());
                            var collectionResults = genericReadData.Invoke(null, new object[] { reader });
                            collection.SetValue(result, collectionResults);
                        }
                    }
                }

                return result;
            }
        }
    }
}
