using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using System.Data;

namespace ConsoleServer
{
    // Класс для работы с пересылаемыми файлами. Поскольку File уже занять, назвал Files
    public class Files
    {
        public string Name;             // Название документа. Может быть любым
        public string FileName;         // Имя файла по правилам ОС
        public byte[] Data;             // Само содержимое файла

        // Создаём пустую заготовку только с названием файла
        public Files(string name)
        {
            Name = name;
            FileName = name;
            Data = null;
        }

        // Создаём пустую заготовку с названием и именем файла
        public Files(string name, string filename)
        {
            Name = name;
            FileName = filename;
            Data = null;
        }

        // Создаём файл и наполняем информацией, полученной из другого источника
        public Files(string name, string filename, byte[] info)
        {
            Name = name;
            FileName = filename;
            Data = info;
        }

        // Сохраняем содержимое файла на диск. Требуется адрес для полного пути.
        public void Save(string Address = "")
        {
            FileStream fs = new FileStream(Address == "" ? FileName : Address + "\\" + FileName, 
                FileMode.Create, FileAccess.Write);
            fs.Write(Data, 0, Data.Length);
            fs.Flush();
            fs.Close();
        }

        // Заносит информацию о файле в БД. В будущем сделаю шифрование информации.
        public void Add_To_DB(DB DataBase, AES_Data AES, int Lab, int Person)
        {
            string queryString = @"INSERT INTO `files` 
(`name`, `file_name`, `info`, `laboratory`, `person`)
VALUES ('" + Name + "', '" + FileName + "', @Info, " + Lab.ToString() + ", " + Person.ToString() + ");";

            MySqlCommand com = DataBase.MakeCommandObject(queryString);
            com.Parameters.AddWithValue("@Info", Data);
            com.ExecuteNonQuery();
        }

        // Загружает информацию о файле из БД и создаёт новый Files объект
        public static Files Read_From_DB(DB DataBase, int id, User CurUser)
        {
            //Создаём запрос на поиск
            string queryString = @"SELECT `name`, `file_name`, `info` ";
            queryString += "\nFROM `files` \n";
            queryString += "WHERE " + CurUser.GetSearchRermissions();
            DataTable dt = DataBase.Query(queryString);

            if (dt.Rows.Count == 0) return null;

            return new Files(dt.Rows[0].ItemArray[0].ToString(), dt.Rows[0].ItemArray[1].ToString(),
                dt.Rows[0].ItemArray[2] as byte[]);
        }

        // Загружаем файл с диска и создаём новый объект Files 
        public static Files Load(string FileName, string Address = "")
        {
            FileStream fs = new FileStream(Address == "" ? FileName : Address + "\\" + FileName, 
                FileMode.Open, FileAccess.Read);
            byte[] data = new byte[fs.Length];
            fs.Read(data, 0, data.Length);
            fs.Flush();
            fs.Close();

            return new Files(FileName, FileName, data);
        }
    }
}
