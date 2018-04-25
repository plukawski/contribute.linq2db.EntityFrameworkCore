﻿using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace LinqToDB.EntityFrameworkCore
{
	using Data;
	using DataProvider;
	using Linq;
	using Mapping;
	using Metadata;

	[PublicAPI]
	public static class Linq2DbTools
	{
		private static ILinq2DbTools _implementation;

		/// <summary>
		/// Allows changing Linq2DbTools behaviour
		/// </summary>
		public static ILinq2DbTools Implementation
		{
			get => _implementation;
			set
			{
				_implementation = value ?? throw new ArgumentNullException(nameof(value)); 
				_metadataReaders.Clear();
				_defaultMeadataReader = new Lazy<IMetadataReader>(() => Implementation.CreateMetadataReader(null));
			}
		}

		private static readonly ConcurrentDictionary<IModel, IMetadataReader> _metadataReaders = new ConcurrentDictionary<IModel, IMetadataReader>();

		private static Lazy<IMetadataReader> _defaultMeadataReader;

	    static Linq2DbTools()
	    {
			Implementation = new Linq2DbToolsImplDefault();
	    }

		public static IMetadataReader GetMetadataReader([JetBrains.Annotations.CanBeNull] IModel model)
		{
			if (model == null)
				return _defaultMeadataReader.Value;

			return _metadataReaders.GetOrAdd(model, m => Implementation.CreateMetadataReader(model));
		}

		public static DbContextOptions GetContextOptions(DbContext context)
		{
			return Implementation.GetContextOptions(context);
		}

		public static EfProviderInfo GetEfProviderInfo(DbContext context)
	    {
		    var info = new EfProviderInfo
		    {
				Connection = context.Database.GetDbConnection(),
				Context = context,
				Options = GetContextOptions(context)
		    };

		    return info;
	    }

		public static EfProviderInfo GetEfProviderInfo(DbConnection connection)
	    {
		    var info = new EfProviderInfo
		    {
				Connection = connection,
				Context = null,
				Options = null
		    };

		    return info;
	    }

		public static EfProviderInfo GetEfProviderInfo(DbContextOptions options)
	    {
		    var info = new EfProviderInfo
		    {
				Connection = null,
				Context = null,
				Options = options
		    };

		    return info;
	    }

		public static IDataProvider GetDataProvider(EfProviderInfo info)
	    {
		    var provider = Implementation.GetDataProvider(info);

		    if (provider == null)
				throw new Exception("Can not detect provider from Entity Framework or provider not supported");

			return provider;
	    }

		public static MappingSchema GetMappingSchema(IModel model)
	    {
		    return Implementation.GetMappingSchema(model, GetMetadataReader(model));
	    }

		public static Expression TransformExpression(Expression expression, IDataContext dc)
		{
			return Implementation.TransformExpression(expression, dc);
		}

	    public static DataConnection CreateLinqToDbConnection(this DbContext context,
		    IDbContextTransaction transaction = null)
	    {
		    if (context == null) throw new ArgumentNullException(nameof(context));

		    var info = GetEfProviderInfo(context);

		    DataConnection dc = null;

		    transaction = transaction ?? context.Database.CurrentTransaction;

			if (transaction != null)
			{
				var dbTrasaction = transaction.GetDbTransaction();
				dc = new DataConnection(GetDataProvider(info), dbTrasaction);
			}

			if (dc == null)
				dc = new DataConnection(GetDataProvider(info), context.Database.GetDbConnection());

		    var mappingSchema = GetMappingSchema(context.Model);
			if (mappingSchema != null)
				dc.AddMappingSchema(mappingSchema);

			return dc;
	    }

	    public static DataConnection CreateLinq2DbConnectionDetached([JetBrains.Annotations.NotNull] this DbContext context)
	    {
		    if (context == null) throw new ArgumentNullException(nameof(context));

		    var info = GetEfProviderInfo(context);
		    var connectionInfo = GetConnectionInfo(info);
			var dataProvider = GetDataProvider(info);

			var dc = new DataConnection(dataProvider, connectionInfo.ConnectionString);

		    var mappingSchema = GetMappingSchema(GetModel(GetContextOptions(context)));
			if (mappingSchema != null)
				dc.AddMappingSchema(mappingSchema);

			return dc;
	    }

		public static EfConnectionInfo GetConnectionInfo(EfProviderInfo info)
		{
			var connection = info.Connection;
			var connectionString = connection?.ConnectionString;

			if (connection != null && connectionString != null)
				return new EfConnectionInfo { Connection = connection, ConnectionString = connectionString };

			var extracted = Implementation.ExtractConnectionInfo(info.Options);

			return new EfConnectionInfo
			{
				Connection = connection ?? extracted?.Connection,
				ConnectionString = extracted?.ConnectionString
			};
		}

		public static IModel GetModel(DbContextOptions options)
		{
			return Implementation.ExtractModel(options);
		}

		public static DataConnection CreateLinqToDbConnection(this DbContextOptions options)
		{
			var info = GetEfProviderInfo(options);

			DataConnection dc = null;

			var connectionInfo = GetConnectionInfo(info);
			var dataProvider = GetDataProvider(info);
			if (connectionInfo.Connection != null)
				dc = new DataConnection(dataProvider, connectionInfo.Connection);
			else if (connectionInfo.ConnectionString != null)
				dc = new DataConnection(dataProvider, connectionInfo.ConnectionString);

			if (dc == null)
				throw new Exception("Can not extract connection info from DbContextOptions");

			var mappingSchema = GetMappingSchema(GetModel(options));
			if (mappingSchema != null)
				dc.AddMappingSchema(mappingSchema);

			return dc;
		}

		/// <summary>
		/// Converts Entity Framework's query to LinqToDb realisation.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="query"></param>
		/// <param name="dc"></param>
		/// <returns></returns>
		public static IQueryable<T> ToLinqToDb<T>(this IQueryable<T> query, IDataContext dc)
	    {
		    var newExpression = TransformExpression(query.Expression, dc);

		    return Internals.CreateExpressionQueryInstance<T>(dc, newExpression);
	    }

	    public static DbContext GetCurrentContext(IQueryable query)
	    {
		    return Implementation.GetCurrentContext(query);
	    }
    }
}
