using ConsoleServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Extentions;
using System.Data;

namespace Commands
{
    class FileEngine : ExecutableCommand, IStandartCommand
    {
        // Команды по журналам
        public const string Help = "help";              // Справка по использованию журнала
        public const string GetName = "name";          // Показать список сессий
        public const string Get = "get";            // Показать список запросов.
        public const string Send = "send";           // Показать список запросов.

        public FileEngine(DB dataBase) : base(dataBase)
        {
            Name = "file";              // Название
        }

        /// <summary>
        /// Выполнение операций классом. 
        /// </summary>
        /// <param name="handler">Сокет, через который посылается ответ</param>
        /// <param name="CurUser">Пользователь</param>
        /// <param name="DataBase">База данных, из которой берётся информация</param>
        /// <param name="Command">Операция для выполнения</param>
        /// <param name="Params">Параметры операции</param>
        public void Execute(Socket handler, User CurUser, string[] Command, 
            string[] Params)
        {
            if (Command.Length == 1)
            {
                SendHelp(handler, CurUser);
                return;
            }

            switch (Command[1].ToLower())
            {
                case Help: SendHelp(handler, CurUser); break;
                case GetName: GetFileName(handler, CurUser, Params); break; // Послать название файла
                case Get: SendFile(handler, CurUser, Params); break; // Команда принять, значит мы посылаем
                case Send: GetFile(handler, CurUser, Params); break; // Команда Послать, значит мы принимаем
                default: CurUser.Transport.SimpleMsg(handler, "Unknown command"); break;
            }
        }

        /// <summary>
        /// Показывает справку о команде
        /// </summary>
        /// <param name="handler"></param>
        private void SendHelp(Socket handler, User CurUser)
        {
            CurUser.Transport.SimpleMsg(handler, @"Work with files. Not for console use exept:
 - name [file id] - gives the name of file");
        }

        /// <summary>
        /// Отсылает файл клиенту
        /// </summary>
        /// <param name="handler">Сокет, через который передаётся файл</param>
        /// <param name="CurUser">Пользователь</param>
        /// <param name="DataBase">База данных</param>
        /// <param name="FileID">Номер файла</param>
        private void SendFile(Socket handler, User CurUser, string[] Params)
        {
            int FileID = Params[0].ToInt();
            Files FileToSend = Files.Read_From_DB(DataBase, FileID, CurUser);
            /*using (FileStream FS = new FileStream("temp.dat", FileMode.Create))
            {
                FS.Write(CurUser.Transport.Crypt.AesKey, 0, CurUser.Transport.Crypt.AesKey.Count());
                FS.Write(CurUser.Transport.Crypt.AesIV, 0, CurUser.Transport.Crypt.AesIV.Count());
                FS.Close();
            }*/
            CurUser.Transport.SendBinaryData(handler, FileToSend.Data);

        }

        /// <summary>
        /// Передаёт клиенту размер файла
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="FileToSend"></param>
        private void SendFileSize(Socket handler, User CurUser, Files FileToSend)
        {
            CurUser.Transport.SimpleMsg(handler, new string[] { FileToSend.FileName,
            FileToSend.EncryptedDataStream.Length.ToString() });           
        }

        /// <summary>
        /// Программа для приёма файла от клиента
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="DataBase"></param>
        /// <param name="Params"></param>
        private void GetFile(Socket handler, User CurUser, string[] Params)
        {
            // Начальная инициация переменных, чтобы из IF(){} вышли
            int FileSize = -1;
            string Caption = "";
            string FileName = "";
            int MoleculeID = -1;

            // Посмотрим все доп. параметры
            for (int i = 0; i < Params.Count(); i++)
            {
                string[] Param = Params[i].ToLower().Split(' '); // Доп. параметр от значения отделяется пробелом
                if (Param[0] == "size") FileSize = Param[1].ToInt();       // Размер файла
                if (Param[0] == "caption") Caption = Param[1];             // Название файла
                if (Param[0] == "filename") FileName = Param[1];           // Имя файла
                if (Param[0] == "molecule") MoleculeID = Param[1].ToInt(); // Номер молекулы
            }

            byte[] ResFile = new byte[FileSize];

            Stream ms = new MemoryStream();
            for (int i = 0; i < FileSize; i += 1024)
            {
                int Size = 1024;
                if (FileSize - i < 1024) Size = FileSize - i;
                byte[] Block = new byte[Size];
                handler.Receive(Block, Size, SocketFlags.None);
                ms.Write(Block, 0, Size);
            }
            ms.Position = 0;
            ms.Read(ResFile, 0, FileSize);

            Files FileToAdd = new Files(Caption, FileName, ResFile);
            int FileID = FileToAdd.Add_To_DB(DataBase, Program.CommonAES, CurUser.GetID(), 
                CurUser.GetLaboratory());

            DataBase.ExecuteQuery(@"INSERT INTO `files_to_molecules` (`file`, `molecule`)
VALUES (" + FileID + ", " + MoleculeID + ")");

            CurUser.Transport.SimpleMsg(handler, FileID.ToString());
        }

        /// <summary>
        /// Получить имя файла из БД по его ID"
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="FileID"></param>
        private void GetFileName(Socket handler, User CurUser, string[] Params)
        {
            string FileID = Params[0];
            DataTable NewFile = DataBase.Query(@"SELECT `file_name` FROM files WHERE `id`=" +
                            FileID + @" LIMIT 1;");
            string Out;
            if (NewFile.Rows.Count == 0) { Out = "Файл отсутствует"; }
            else { Out = NewFile.Rows[0].ItemArray[0].ToString(); }

            CurUser.Transport.SimpleMsg(handler, Out);
        }
    }
}
