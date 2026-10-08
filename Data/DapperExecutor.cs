using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using System.IO;
using System;
using System.Text.RegularExpressions;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using System.Diagnostics;

namespace API_AMNOTE_WEB.Data
{
    public class DapperExecutor
    {
        private readonly IDapperContext _context;

        public DapperExecutor(IDapperContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IDbConnection GetOpenConnection(Net_DB db = Net_DB.Net_DB_Company, string? DBName = null)
        {
            var conn = _context.CreateConnection(db, DBName);
            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }
            return conn;
        }

        public async Task<DapperSession> CreateSessionAsync(Net_DB db = Net_DB.Net_DB_Company, string? DBName = null)
        {
            var conn = _context.CreateConnection(db, DBName);
            if (conn.State != ConnectionState.Open)
            {
                if (conn is System.Data.Common.DbConnection dbConn)
                {
                    await dbConn.OpenAsync();
                }
                else
                {
                    conn.Open();
                }
            }

            var tx = conn.BeginTransaction();
            return new DapperSession(conn, tx, this);
        }

        public async Task<int> ExecuteAsync(Net_DB db, string sql, object param = null, string? sDBName = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);
            using var conn = _context.CreateConnection(db, sDBName);
            var result = await conn.ExecuteAsync(sql, param, commandTimeout: commandTimeout);
            sw.Stop();
            return result;
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(Net_DB db, string sql, object param = null, string? sDBName = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);
            using var conn = _context.CreateConnection(db, sDBName);
            var result = await conn.QueryAsync<T>(sql, param, commandTimeout: commandTimeout);
            sw.Stop();

            return result;
        }

        public async Task<T> QueryFirstAsync<T>(Net_DB db, string sql, object param = null, string? sDBName = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);
            using var conn = _context.CreateConnection(db, sDBName);
            var result = await conn.QueryFirstAsync<T>(sql, param, commandTimeout: commandTimeout);
            sw.Stop();

            return result;
        }

        public async Task<T> QuerySingleAsync<T>(Net_DB db, string sql, object param = null, string? sDBName = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);
            using var conn = _context.CreateConnection(db, sDBName);
            var result = await conn.QuerySingleAsync<T>(sql, param, commandTimeout: commandTimeout);
            sw.Stop();
            return result;
        }

        public async Task<(IReadOnlyList<T1> First, IReadOnlyList<T2> Second)> QueryMultipleAsync<T1, T2>(Net_DB db, string sql, object param = null, string? sDBName = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param);
            using var conn = _context.CreateConnection(db, sDBName);
            using var grid = await conn.QueryMultipleAsync(sql, param, commandTimeout: commandTimeout);
            var first = (await grid.ReadAsync<T1>()).ToList();
            var second = (await grid.ReadAsync<T2>()).ToList();
            sw.Stop();
            Common.LogQuery($"-- DONE {sql} ({sw.ElapsedMilliseconds} ms)", null);
            return (first, second);
        }

        public async Task<(IReadOnlyList<T1> First, IReadOnlyList<T2> Second, IReadOnlyList<T3> Third)> QueryMultipleAsync<T1, T2, T3>(
            Net_DB db,
            string sql,
            object param = null,
            string? sDBName = null,
            int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param);
            using var conn = _context.CreateConnection(db, sDBName);
            using var grid = await conn.QueryMultipleAsync(sql, param, commandTimeout: commandTimeout);
            var first = (await grid.ReadAsync<T1>()).ToList();
            var second = (await grid.ReadAsync<T2>()).ToList();
            var third = (await grid.ReadAsync<T3>()).ToList();
            sw.Stop();
            Common.LogQuery($"-- DONE {sql} ({sw.ElapsedMilliseconds} ms)", null);
            return (first, second, third);
        }

        public async Task<IEnumerable<dynamic>> QueryAsync(Net_DB db, string sql, object param = null, CommandType? commandType = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);
            using var conn = _context.CreateConnection(db);
            if (conn.State != ConnectionState.Open) conn.Open();
            var result = await conn.QueryAsync(sql, param, commandTimeout: commandTimeout, commandType: commandType);
            sw.Stop();
            return result;
        }
    }
}