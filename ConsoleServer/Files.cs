using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using System.Data;

namespace ConsoleServer
{
    public class Files
    {
        public static void Save(string FileName, byte[] Info)
        {
            FileStream fs = new FileStream(FileName, FileMode.Create, FileAccess.Write);
            fs.Write(Info, 0, Info.Length);
            fs.Flush();
            fs.Close();
        }

        public static void Add_To_DB(MySqlConnection con, AES_Data AES, 
            string Name, string FileName, byte[] Info, int Lab, int Person)
        {
            string queryString = @"INSERT INTO `files` 
(`name`, `file_name`, `info`, `laboratory`, `person`)
VALUES ('" + Name + "', '" + FileName + "', @Info, " + Lab.ToString() + ", " + Person.ToString() + ");";

            MySqlCommand com = new MySqlCommand(queryString, con);
            if (con.State == ConnectionState.Closed) { con.Open(); };
            com.Parameters.AddWithValue("@Info", Info);

            com.ExecuteNonQuery();
            con.Close();
        }

        public static byte[] Load(string FileName)
        {
            FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
            byte[] data = new byte[fs.Length];
            fs.Read(data, 0, data.Length);
            fs.Flush();
            fs.Close();

            return data;
        }
    }
}
