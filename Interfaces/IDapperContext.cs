using API_AMNOTE_WEB.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IDapperContext
    {
        public IDbConnection CreateConnection(Net_DB _Net_DB, string ? DBName = null);
    }
}