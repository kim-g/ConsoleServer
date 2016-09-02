using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace ConsoleServer
{
    // Выполняет стандартные действия по БД
    public class DB
    {
        //Объекты БД
        MySqlConnectionStringBuilder mysqlCSB;
        MySqlConnection con;

        // Запрос в БД. Выдаёт таблицу.
        public DataTable Query(string queryString)
        {
            DataTable dt = new DataTable();
            // Создание команды MySQL
            MySqlCommand com = new MySqlCommand(queryString, con);

            // Выполнение запроса
            try
            {
                ConOpen();

                using (MySqlDataReader dr = com.ExecuteReader())
                {
                    if (dr.HasRows)
                    {
                        dt.Load(dr);
                    }
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            ConClose();
            return dt;
        }

        // Запрос в БД. Выдаёт MySqlCommand объект для сложных запросов.
        public MySqlCommand MakeCommandObject(string queryString)
        {
            MySqlCommand com = new MySqlCommand(queryString, con);
            ConOpen();
            return com;
        }

        // Запрос в БД без выдачи результата.
        public void ExecuteQuery(string QueryString)
        {
            MySqlCommand com = new MySqlCommand(QueryString, con);

            try
            {
                ConOpen();
                com.ExecuteNonQuery();
                con.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        // Конструктор. Требует параметров БД для соединения. Создаёт MySqlConnection объект внутри себя.
        public DB(string DB_Server, string DB_Name, string DB_User, string DB_Pass)
        {
            mysqlCSB = new MySqlConnectionStringBuilder();
            mysqlCSB.Server = DB_Server;
            mysqlCSB.Database = DB_Name;
            mysqlCSB.UserID = DB_User;
            mysqlCSB.Password = DB_Pass;

            using (con = new MySqlConnection())
            {
                con.ConnectionString = mysqlCSB.ConnectionString;
            }

        }

        // Открывает соединение, если оно закрыто
        public void ConOpen()
        {
            if (con.State == ConnectionState.Closed) { con.Open(); };
        }

        // Закрывает соединение, если оно открыто.
        public void ConClose()
        {
            if (con.State == ConnectionState.Open) { con.Close(); };
        }

    }
}
