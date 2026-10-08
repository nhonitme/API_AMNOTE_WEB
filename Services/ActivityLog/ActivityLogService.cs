using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Dapper;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;

namespace API_AMNOTE_WEB.Services.ActivityLog
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly IHttpContextAccessor _http;

        public ActivityLogService(IHttpContextAccessor http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task LogAsync(IDbConnection connection, IDbTransaction? transaction, string companyCd, string actionType, string moduleName, string tableName, string recordId, string oldData, string newData, string description)
        {
            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                var ctx = _http.HttpContext;
                var req = ctx?.Request;
                var userIdText = ctx?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
                var username = ctx?.User?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
                // Request-only IP — never call external IP services (hangs / returns server IP).
                var userIp = ClientPublicIpResolver.ResolveFromRequest(ctx);
                var userAgent = ResolveUserAgent(req);
                var transactionId = Guid.NewGuid().ToString();

                long.TryParse(userIdText, out var userId);

                const string proc = @"CALL ins_user_activity_log( @p_company_cd, @p_user_id, @p_username, @p_user_ip, @p_user_agent, @p_action_type, @p_module_name, @p_table_name, @p_record_id, @p_old_data, @p_new_data, @p_description, @p_transaction_id )";

                await connection.ExecuteAsync(proc, new
                {
                    p_company_cd = string.IsNullOrWhiteSpace(companyCd) ? Common.GetCompanyCode() : companyCd.Trim(),
                    p_user_id = userId,
                    p_username = username,
                    p_user_ip = userIp,
                    p_user_agent = userAgent,
                    p_action_type = actionType?.ToUpperInvariant(),
                    p_module_name = moduleName,
                    p_table_name = tableName,
                    p_record_id = recordId,
                    p_old_data = oldData,
                    p_new_data = newData,
                    p_description = description,
                    p_transaction_id = transactionId
                }, transaction, commandTimeout: 15);
            }
            catch
            {
            }
        }

        private static string ResolveUserAgent(HttpRequest? req)
        {
            var parts = new List<string>();
            AddHeader(parts, req, "User-Agent");
            AddHeader(parts, req, "X-FingerprintJS-Visitor-Id", "FP");
            AddHeader(parts, req, "X-Visitor-Id", "Visitor");
            AddHeader(parts, req, "sec-ch-ua", "CH");
            AddHeader(parts, req, "sec-ch-ua-platform", "Platform");
            AddHeader(parts, req, "sec-ch-ua-model", "Model");
            parts.Add($"Host:{Environment.MachineName}");
            return string.Join(" | ", parts);
        }

        private static void AddHeader(List<string> parts, HttpRequest? req, string name, string? label = null)
        {
            var value = req?.Headers[name].ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            parts.Add(string.IsNullOrWhiteSpace(label) ? value : $"{label}:{value}");
        }
    }
}
