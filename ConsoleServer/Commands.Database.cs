using ConsoleServer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Commands
{
    class Database : ExecutableCommand, IStandartCommand
    {
        public const string Help = "help";    // Справка по использованию БД
        public const string LastID = "show_last_id";    // Показать последний использованный ID
        public const string StatusList = "status_list"; // Получить список статусов

        public Database(DB dataBase) : base(dataBase)
        {
            Name = "database";
        }

        /// <summary>
        /// Реализация подкоманд "database"
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Command"></param>
        /// <param name="Params"></param>
        /// <param name="Param1"></param>
        /// <param name="Param2"></param>
        public void Execute(Socket handler, User CurUser, string[] Command, 
            string[] Params)
        {
            if (Command.Length == 1)
            {
                SendHelp(handler);
                return;
            }

            switch (Command[1].ToLower())
            {
                case Help: SendHelp(handler); break;
                case LastID: ShowLastID(handler, CurUser); break;
                case StatusList: ShowStatusList(handler); break;
                default: SimpleMsg(handler, "Unknown command"); break;
            }
        }

        /// <summary>
        /// Отправить на сервер список возможных статусов из БД
        /// </summary>
        /// <param name="handler">Сокет, через который отправляется сообщение</param>
        private void ShowStatusList(Socket handler)
        {
            List<string> Res = GetRows("SELECT * FROM `status`");
            SendMsg(handler, Answer.StartMsg);
            for (int i = 0; i < Res.Count; i++)
                SendMsg(handler, Res[i]);
            SendMsg(handler, Answer.EndMsg);
        }

        /// <summary>
        /// Показать последний выданный ID
        /// </summary>
        /// <param name="handler">Сокет, через который отправляется сообщение</param>
        /// <param name="CurUser">Пользователь</param>
        private void ShowLastID(Socket handler, User CurUser)
        {
            DataTable LR = DataBase.Query("SELECT LAST_INSERT_ID()");
            SimpleMsg(handler, LR.Rows[0].ItemArray[0].ToString());
        }

        /// <summary>
        /// Показать справку о команде 
        /// </summary>
        /// <param name="handler">Сокет, через который отправляется сообщение</param>
        private void SendHelp(Socket handler)
        {
            SimpleMsg(handler, @"Command for direct work with database. Possible comands:
 - database.show_last_id - Shows ID of last inserted record
 - database.status_list - Shows list of statuses");
        }

    }
}
