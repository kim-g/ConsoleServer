using ConsoleServer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Commands
{
    class Database : ExecutableCommand
    {
        public const string Name = "database";    // Справка по использованию БД
        public const string Help = "help";    // Справка по использованию БД
        public const string LastID = "show_last_id";    // Показать последний использованный ID
        public const string StatusList = "status_list"; // Получить список статусов

        public static void Execute(Socket handler, User CurUser, DB DataBase, string[] Command, 
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
                case LastID: ShowLastID(handler, CurUser, DataBase); break;
                case StatusList: ShowStatusList(handler); break;
                default: SimpleMsg(handler, "Unknown command"); break;
            }
        }

        private static void ShowStatusList(Socket handler)
        {
            List<string> Res = Program.GetRows("SELECT * FROM `status`");
            SendMsg(handler, Answer.StartMsg);
            for (int i = 0; i < Res.Count; i++)
                SendMsg(handler, Res[i]);
            SendMsg(handler, Answer.EndMsg);
        }

        private static void ShowLastID(Socket handler, User CurUser, DB DataBase)
        {
            DataTable LR = DataBase.Query("SELECT LAST_INSERT_ID()");
            SimpleMsg(handler, LR.Rows[0].ItemArray[0].ToString());
        }

        private static void SendHelp(Socket handler)
        {
            SimpleMsg(handler, @"Command for direct work with database. Possible comands:
 - database.show_last_id - Shows ID of last inserted record
 - database.status_list - Shows list of statuses");
        }

    }
}
