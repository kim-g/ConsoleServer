using ConsoleServer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Extentions;

namespace Commands
{
    class Status : ExecutableCommand, IStandartCommand
    {
        public const string Help = "help";              // Справка по использованию журнала
        public const string GetStatuses = "list";
        public const string Increase_Status = "increase"; // Увеличеть значение статуса соединения

        public Status(DB dataBase) : base(dataBase)
        {
            Name = "status";
        }

        /// <summary>
        /// Выполнение операций классом. 
        /// </summary>
        /// <param name="handler">Сокет, через который посылается ответ</param>
        /// <param name="CurUser">Пользователь</param>
        /// <param name="DataBase">База данных, из которой берётся информация</param>
        /// <param name="Command">Операция для выполнения</param>
        /// <param name="Params">Параметры операции</param>
        public void Execute(Socket handler, User CurUser, string[] Command, string[] Params)
        {
            if (Command.Length == 1)
            {
                SendHelp(handler, CurUser);
                return;
            }

            switch (Command[1].ToLower())
            {
                case Help: SendHelp(handler, CurUser); break;
                case GetStatuses: SendStatusList(handler, CurUser); break;
                case Increase_Status: IncreaseStatus(handler, CurUser, Params); break;
                default: CurUser.Transport.SimpleMsg(handler, "Unknown command"); break;
            }
        }

        /// <summary>
        /// Показывает справку о команде
        /// </summary>
        /// <param name="handler"></param>
        private void SendHelp(Socket handler, User CurUser)
        {
            CurUser.Transport.SimpleMsg(handler, @"System logs. Shows informations aboute program usage. Possible comands:
 - log.sessions - shows sessions history
 - log.queries - shows query history.");
        }

        /// <summary>
        /// Выдаёт список статусов
        /// </summary>
        /// <param name="handler"></param>
        private void SendStatusList(Socket handler, User CurUser)
        {
            List<string> Res = GetRows("SELECT * FROM `status`");
            CurUser.Transport.SimpleMsg(handler, Res);
        }

        /// <summary>
        /// Увеличить статус на 1
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="MolID"></param>
        private void IncreaseStatus(Socket handler, User CurUser, string[] Params)
        {
            string MolID = "";
            // Посмотрим все доп. параметры
            for (int i = 0; i < Params.Count(); i++)
            {
                string[] Param = Params[i].ToLower().Split(' '); // Доп. параметр от значения отделяется пробелом
                if (Param[0] == "molecule") MolID = Param[1];       // Номер молекулы
            }

            DataTable MolStatus = DataBase.Query(@"SELECT `status` FROM `molecules` WHERE (`id`=" +
                            MolID + @") AND (" + CurUser.GetSearchRermissions() + @") LIMIT 1;");
            if (MolStatus.Rows.Count == 0)
            {
                CurUser.Transport.SimpleMsg(handler, "ERROR 101 – Not found or access denied");
                return;
            }
            DataTable NewStatus = DataBase.Query(@"SELECT `next` FROM `status` WHERE (`id`=" +
                            MolStatus.Rows[0].ItemArray[0].ToString() + @") LIMIT 1;");
            if (NewStatus.Rows.Count == 0)
            {
                CurUser.Transport.SimpleMsg(handler, "ERROR 102 – Status not found");
                return;
            }
            if (NewStatus.Rows[0].ItemArray[0].ToString() == "-1")
            {
                CurUser.Transport.SimpleMsg(handler, "ERROR 103 – Maximum status");
                return;
            }

            // Если ни одной ошибки не обнаружено, увеличиваем статус
            DataBase.ExecuteQuery(@"UPDATE `molecules` SET `status` = " +
                NewStatus.Rows[0].ItemArray[0].ToString() + @" WHERE `id` = " + MolID + @" LIMIT 1;");
            CurUser.Transport.SimpleMsg(handler, "OK");
        }
    }
}
