namespace JEWELLBISREACT.DBConnection
{
    public static class DBHelper
    {
        public static string GetConnection()
        {
            string connection = @"Data Source=DIKSHISERVER,1344;Initial Catalog=MLVYS;User ID=sa;Password=Varsha@123#$;Trust Server Certificate=True";
            return connection; 
        }
    }
}
