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

        /// <summary>
        /// Запрос в БД. Выдаёт таблицу.
        /// </summary>
        /// <param name="queryString">Запрос</param>
        /// <returns></returns>
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

        /// <summary>
        /// Запрос в БД. Выдаёт MySqlCommand объект для сложных запросов.
        /// </summary>
        /// <param name="queryString">Запрос</param>
        /// <returns></returns>
        public MySqlCommand MakeCommandObject(string queryString)
        {
            MySqlCommand com = new MySqlCommand(queryString, con);
            ConOpen();
            return com;
        }

        /// <summary>
        /// Запрос в БД без выдачи результата.
        /// </summary>
        /// <param name="QueryString">Запрос</param>
        /// <returns></returns>
        public int ExecuteQuery(string QueryString)
        {
            MySqlCommand com = new MySqlCommand(QueryString, con);
            int result= -1;

            try
            {
                ConOpen();
                result = com.ExecuteNonQuery();
                con.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Конструктор. Требует параметров БД для соединения. Создаёт MySqlConnection объект внутри себя.
        /// </summary>
        /// <param name="DB_Server"></param>
        /// <param name="DB_Name"></param>
        /// <param name="DB_User"></param>
        /// <param name="DB_Pass"></param>
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

        /// <summary>
        /// Открывает соединение, если оно закрыто
        /// </summary>
        public void ConOpen()
        {
            if (con.State == ConnectionState.Closed) { con.Open(); };
        }

        /// <summary>
        /// Закрывает соединение, если оно открыто.
        /// </summary>
        public void ConClose()
        {
            if (con.State == ConnectionState.Open) { con.Close(); };
        }

        /// <summary>
        /// Выдаёт количество записей
        /// </summary>
        /// <param name="Table">Название таблицы</param>
        /// <param name="Where">Условия</param>
        /// <returns></returns>
        public int RecordsCount(string Table, string Where)
        {
            DataTable DT = Query("SELECT Count(*) FROM `" + Table + "` WHERE " + Where + ";");
            return Convert.ToInt32( DT.Rows[0].ItemArray[0] );
        }

        /// <summary>
        /// Выдаёт первую запись в виде List string
        /// </summary>
        /// <param name="QueryString"></param>
        /// <returns></returns>
        public List<string> QueryOne(string QueryString)
        {
            DataTable Table = Query(QueryString);
            if (Table.Rows.Count == 0) return null;

            List<string> Res = new List<string>(); 
            foreach (var El in Table.Rows[0].ItemArray)
            {
                Res.Add(El.ToString());
            }
            return Res;
        }

        /// <summary>
        /// Выдаёт ID последнего добавленного элемента
        /// </summary>
        /// <returns></returns>
        public int GetLastID()
        {
            DataTable LR = Query("SELECT LAST_INSERT_ID()");
            return Convert.ToInt32(LR.Rows[0].ItemArray[0]);
        }

    }
}
