using System;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dapper;
using System.Diagnostics;
using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Data
{
    public class DapperSession : IAsyncDisposable
    {
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;
        private readonly DapperExecutor _executor;

        public DapperSession(IDbConnection connection, IDbTransaction transaction, DapperExecutor executor)
        {
            _connection = connection;
            _transaction = transaction;
            _executor = executor;
        }

        public IDbConnection Connection => _connection;
        public IDbTransaction Transaction => _transaction;

        public async Task<int> ExecuteAsync(string sql, object param = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);
            var result = await _connection.ExecuteAsync(sql, param, _transaction, commandTimeout: commandTimeout);
            sw.Stop();

            return result;
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);
            var result = await _connection.QueryAsync<T>(sql, param, _transaction, commandTimeout, commandType);
            sw.Stop();
            return result;
        }

        public async Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object param = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);
            var result = await _connection.QueryMultipleAsync(sql, param, _transaction, commandTimeout, commandType);
            sw.Stop();

            return result;
        }

        public async Task<T> QuerySingleAsync<T>(string sql, object param = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);

            var result = await _connection.QuerySingleAsync<T>(sql, param, _transaction, commandTimeout: commandTimeout);

            sw.Stop();
            return result;
        }

        public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object param = null, int? commandTimeout = null)
        {
            var sw = Stopwatch.StartNew();
            Common.LogQuery(sql, param, sw.ElapsedMilliseconds);
            var result = await _connection.QueryFirstOrDefaultAsync<T>(sql, param, _transaction, commandTimeout: commandTimeout);
            sw.Stop();
            return result;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _transaction?.Dispose();
            }
            catch { }
            try
            {
                _connection?.Dispose();
            }
            catch { }
            await Task.CompletedTask;
        }

        public void Commit() => _transaction?.Commit();

        public void Rollback() => _transaction?.Rollback();
    }
}
