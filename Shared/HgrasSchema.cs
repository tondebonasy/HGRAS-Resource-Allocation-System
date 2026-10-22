using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;

namespace Hgras.Network
{
    public static class HgrasSchema
    {
        private static readonly object Gate=new object();
        private static readonly HashSet<string> Ready=new HashSet<string>();
        public static void Ensure(string connectionString,string migrationPath)
        {
            lock(Gate)
            {
                if(Ready.Contains(connectionString)) return;
                if(!File.Exists(migrationPath)) throw new InvalidOperationException("The network database update was not included in this deployment.");
                using(var connection=new SqlConnection(connectionString))
                using(var command=new SqlCommand(File.ReadAllText(migrationPath),connection))
                {
                    command.CommandTimeout=60;
                    connection.Open();
                    using(var check=new SqlCommand("SELECT CASE WHEN (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name IN ('HgrasGroup','HgrasFacility','HgrasMember','HgrasDepartment','HgrasStock','HgrasTransfer','HgrasAudit','HgrasOperation','HgrasNotice','HgrasNoticeRead'))=10 AND COL_LENGTH('dbo.HgrasTransfer','ReceivedQuantity') IS NOT NULL THEN 1 ELSE 0 END",connection))
                        if(Convert.ToInt32(check.ExecuteScalar())!=1) command.ExecuteNonQuery();
                }
                Ready.Add(connectionString);
            }
        }
    }
}
