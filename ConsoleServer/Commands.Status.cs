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
                SendHelp(handler);
                return;
            }

            switch (Command[1].ToLower())
            {
                case Help: SendHelp(handler); break;
                case GetStatuses: SendStatusList(handler); break;
                case Increase_Status: IncreaseStatus(handler, CurUser, Params); break;
                default: SimpleMsg(handler, "Unknown command"); break;
            }
        }

        /// <summary>
        /// Показывает справку о команде
        /// </summary>
        /// <param name="handler"></param>
        private void SendHelp(Socket handler)
        {
            SimpleMsg(handler, @"System logs. Shows informations aboute program usage. Possible comands:
 - log.sessions - shows sessions history
 - log.queries - shows query history.");
        }

        /// <summary>
        /// Выдаёт список статусов
        /// </summary>
        /// <param name="handler"></param>
        private void SendStatusList(Socket handler)
        {
            List<string> Res = GetRows("SELECT * FROM `status`");
            SendMsg(handler, Commands.Answer.StartMsg);
            for (int i = 0; i < Res.Count; i++)
                SendMsg(handler, Res[i]);
            SendMsg(handler, Commands.Answer.EndMsg);
        }

        /// <summary>
        /// Выдаёт список строк из базы
        /// </summary>
        /// <param name="Query">SQL Запрос</param>
        /// <returns></returns>
        private List<string> GetRows(string Query)
        {
            List<string> Result = new List<string>();

            // Получение данных из БД по запросу
            DataTable DT = DataBase.Query(Query);

            if (DT.Rows.Count > 0)  // Выводим результат
            {
                for (int i = 0; i < DT.Rows.Count; i++)
                {
                    for (int j = 0; j < DT.Columns.Count; j++)
                    {
                        Result.Add(NotNull(DT.Rows[i].ItemArray[j].ToString().Trim("\n"[0])));
                    }
                }

            }
            else
            {
                for (int j = 1; j < DT.Columns.Count; j++)
                {
                    Result.Add("ERROR 2 – Data not found");
                }
            }

            return Result;
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
                SimpleMsg(handler, "ERROR 101 – Not found or access denied");
                return;
            }
            DataTable NewStatus = DataBase.Query(@"SELECT `next` FROM `status` WHERE (`id`=" +
                            MolStatus.Rows[0].ItemArray[0].ToString() + @") LIMIT 1;");
            if (NewStatus.Rows.Count == 0)
            {
                SimpleMsg(handler, "ERROR 102 – Status not found");
                return;
            }
            if (NewStatus.Rows[0].ItemArray[0].ToString() == "-1")
            {
                SimpleMsg(handler, "ERROR 103 – Maximum status");
                return;
            }

            // Если ни одной ошибки не обнаружено, увеличиваем статус
            DataBase.ExecuteQuery(@"UPDATE `molecules` SET `status` = " +
                NewStatus.Rows[0].ItemArray[0].ToString() + @" WHERE `id` = " + MolID + @" LIMIT 1;");
            SimpleMsg(handler, "OK");
        }
    }
}
