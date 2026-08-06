using System.Reflection;
using DbUp;
using DbUp.Postgresql;

namespace MP.Auth.Infrastructure.Db;

public static class DbMigrator
{
    /// <summary>启动时执行 Scripts/ 下按文件名顺序排列的嵌入 SQL 脚本，幂等。</summary>
    public static void Run(string connectionString)
    {
        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
            throw new InvalidOperationException("数据库迁移失败", result.Error);
    }
}
