using ConsoleServer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Commands
{
    /// <summary>
    /// Взаимодействие с журналом операций
    /// </summary>
    class Log : ExecutableCommand, IStandartCommand
    {
        // Команды по журналам
        public const string Help = "help";              // Справка по использованию журнала
        public const string Session = "sessions";       // Показать список сессий
        public const string Query = "queries";          // Показать список запросов.

        public Log(DB dataBase) : base(dataBase)
        {
            Name = "log";               // Название
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
                case Session: ShowSessions(handler, CurUser, Params); break;
                case Query: ShowQueries(handler, CurUser, Params); break;
                default: CurUser.Transport.SimpleMsg(handler, "Unknown command"); break;
            }
        }

        /// <summary>
        /// Показывает справку о команде
        /// </summary>
        /// <param name="handler"></param>
        private void SendHelp(Socket handler, User CurUser) => CurUser.Transport.SimpleMsg(handler, @"System logs. Shows informations aboute program usage. Possible comands:
 - log.sessions - shows sessions history
 - log.queries - shows query history.");

        /// <summary>
        /// Отсылает клиенту список запросов по параметрам
        /// </summary>
        /// <param name="CurUser"></param>
        /// <param name="curUser"></param>
        /// <param name="dataBase"></param>
        /// <param name="params"></param>
        private void ShowQueries(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ, то ничего не покажем!
            if (!CurUser.IsAdmin()) return;

            // Взять всё из журнала и...
            string Query = @"SELECT `queries`.`id`, `persons`.`login`, `session`, `ip`, `date`, `command`, `parameters`, `comment` 
FROM mol_base.queries
INNER JOIN persons ON(persons.id = queries.user)";

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string UserName = "";
            string Session = "";
            string IP = "";
            string Date = "";
            string DateBegin = "";
            string DateEnd = "";
            string Command = "";
            string Parameters = "";
            string Comment = "";
            string Limit = "100";

            // Посмотрим все доп. параметры
            for (int i = 0; i < Params.Count(); i++)
            {
                string[] Param = Params[i].ToLower().Split(' '); // Доп. параметр от значения отделяется пробелом
                if (Param[0] == "person") UserName = Param[1];      // Показать запросы конкретного человека
                if (Param[0] == "session") Session = Param[1];      // Показать запросы в конкретной сессии
                if (Param[0] == "ip") IP = Param[1];                // Показать запросы c конкретного IP
                if (Param[0] == "date") Date = Param[1];            // Показать запросы в конкретный день
                if (Param[0] == "period")                           // Показать запросы в конкретный день
                {
                    DateBegin = Param[1];
                    DateEnd = Param[2];
                }
                if (Param[0] == "command") Command = Param[1];            // Показать запросы с конкретной командой
                if (Param[0] == "parameter") Parameters = Param[1];       // Показать запросы с конкретным параметром (нафиг надо?!)
                if (Param[0] == "comment") Comment = Param[1];            // Показать запросы с конкретым комментарием
                if (Param[0] == "limit") Limit = Param[1];          // Показать конкретное число запросов

                // Служебные
                if (Param[0] == "help")     // Помощь
                {
                    CurUser.Transport.SimpleMsg(handler, @"log.queries shows list of users' queries to server. All queries are logged. There are several filter parameters:

 - person [login] - Show only person's queries;
 - date YYYY-MM-DD - Shows queries that were in this day;
 - perood YYYY-MM-DD YYYY-MM-DD - Shows queries in that period of time;
 - limit [Number] - How many queries to show. Default is 100;
 - session [Number] - queries in session with ID=[Number];
 - ip [IP address or range] - Shows queries from this IP. Examples: '127.0.0.1', '192.168.';
 - command [Command] - Shows queries with certain main command;
 - parameter - Shows command with definite parameter. Only one of them.
 - comment - Shoqs queries with certain comment. Commens starts with symbols '!' - Not important, '!!' - important, '!!!' - very important. You may filter by this symbols.

Parameters may be combined.");
                    return;
                }
            }

            // Если есть условие выборки, добавим WHERE
            if (UserName != "" || Session != "" || IP != "" || Date != "" || DateBegin != ""
                || Command != "" || Parameters != "")
                Query += "\nWHERE TRUE";

            //Выберем отдельного человека
            if (UserName != "")
            {
                string Pers = User.PersonID(handler, UserName, DataBase);
                if (Pers == "NoUser") return;
                Query += " AND (`user` = " + Pers + ")";
            }

            //Выберем сессию
            if (Session != "") Query += " AND (`session` = " + Session + ")";

            // Выберем конкретный день
            if (Date != "")
            {
                Query += @" AND (DATE(`date`) BETWEEN '" + Date + @"  00:00:00' AND '" + Date + @"  23:59:59')";
            }

            // Выберем диапазон дат
            if (DateBegin != "")
            {
                Query += @" AND (DATE(`date`) BETWEEN '" + DateBegin + @"  00:00:00' AND '" + DateEnd + @"  23:59:59')";
            }

            //Выберем IP
            if (IP != "") Query += " AND (`ip` = '" + IP + "')";

            //Выберем команду
            if (Command != "") Query += " AND (`command` = '" + Command + "')";

            //Выберем параметры
            if (Parameters != "") Query += " AND (`paremeters` LIKE '%" + Parameters + "%')";

            //Выберем коммент
            if (Comment != "") Query += " AND (`comment` LIKE '%" + Comment + "%')";

            // Добавим обратную сортировку и лимит
            Query += "\nORDER BY `id` DESC\nLIMIT " + Limit + ";";

            DataTable Res = DataBase.Query(Query);

            // И пошлём всё пользователю.
            List<string> Out = new List<string>();
            Out.Add("| ID     | user            | session | IP              | Date                | Command (Parameters) – Comment");
            Out.Add("|--------|-----------------|---------|-----------------|---------------------|-----------------------------------");

            //Server Fail – quit date of restart
            if (Res.Rows.Count == 0) SendMsg(handler, "Results not found");

            for (int i = 0; i < Res.Rows.Count; i++)
            {
                string msg = "| " + Res.Rows[i].ItemArray[0].ToString() + "\t | ";
                msg += Res.Rows[i].ItemArray[1].ToString() +
                    new String(' ', 15 - Res.Rows[i].ItemArray[1].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[2].ToString() +
                    new String(' ', 7 - Res.Rows[i].ItemArray[2].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[3].ToString() +
                    new String(' ', 15 - Res.Rows[i].ItemArray[3].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[4].ToString() +
                    new String(' ', 19 - Res.Rows[i].ItemArray[4].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[5].ToString() + " (";
                msg += Res.Rows[i].ItemArray[6].ToString().Replace('\n', ' ').Replace('\r', ';') + ") – ";
                msg += Res.Rows[i].ItemArray[7].ToString() + "";
                Out.Add(msg);
            }
            CurUser.Transport.SimpleMsg(handler, Out);
        }

        /// <summary>
        /// Отсылает клиенту список сессий по параметрам
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="curUser"></param>
        /// <param name="dataBase"></param>
        /// <param name="params"></param>
        private void ShowSessions(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ, то ничего не покажем!
            if (!CurUser.IsAdmin()) return;

            // Взять всё из журнала и...
            string Query = @"SELECT `sessions`.`id`, `enter_date`,`quit_date`, `persons`.`login`, `ip`, `reason_quit` 
FROM `sessions`
INNER JOIN `persons` ON (`persons`.`id` = `sessions`.`user`)";

            // Инициация переменных, чтобы из if(){} нормально вышли
            string Person = "";
            string Date = "";
            string DateRangeBegin = "";
            string DateRangeEnd = "";
            string Limit = "";
            string Reason = "";
            string Active = "";
            string IP = "";

            // Посмотрим все доп. параметры
            for (int i = 0; i < Params.Count(); i++)
            {
                string[] Param = Params[i].ToLower().Split(' '); // Доп. параметр от значения отделяется пробелом
                if (Param[0] == "person") Person = Param[1].Trim('\r'); // Для конкретного пользователя
                if (Param[0] == "date") Date = Param[1].Trim('\r');     // Для конкретной даты
                if (Param[0] == "period")                               // Для периода
                {
                    DateRangeBegin = Param[1].Trim('\r');
                    DateRangeEnd = Param[2].Trim('\r');
                }
                if (Param[0] == "limit") Limit = Param[1].Trim('\r'); else Limit = "100";   // Сколько показать
                if (Param[0] == "reason") // Для конкретной причины выхода
                {
                    Reason = Param[1].Trim('\r');
                    for (int j = 2; j < Param.Count(); j++)
                        Reason += " " + Param[j].Trim('\r');
                }
                if (Param[0] == "active") Active = "TRUE";              // Только активных
                if (Param[0] == "ip") IP = Param[1].Trim('\r');     // Для конкретной даты

                // Служебные
                if (Param[0] == "help")     // Помощь
                {
                    CurUser.Transport.SimpleMsg(handler, @"log.sessions shows list of sessions. There are several filter parameters:

 - person [login] - Show only person's sessions;
 - date YYYY-MM-DD - Shows sessions that started or ended in this day;
 - perood YYYY-MM-DD YYYY-MM-DD - Shows sessions in that period of time;
 - ip - Shows sessions from certain IP address or range. Examples: '127.0.0.1', .192.168.'
 - limit [Number] - How many sessions to show. Default is 100;
 - reason [Reason] - Shows sessions, that ended with definite reason;
 - active - Shows only current working sessions.

Parameters may be combined.");
                    return;
                }
            }

            // Если есть условие выборки, добавим WHERE
            if (Person != "" || Date != "" || DateRangeBegin != "" || Reason != "" || Active != "" || IP != "")
                Query += " WHERE TRUE";

            //Выберем отдельного человека
            if (Person != "")
            {
                string Pers = User.PersonID(handler, Person, DataBase);
                if (Pers == "NoUser") return;
                Query += " AND (`user` = " + Pers + ")";
            }

            // Выберем конкретный день
            if (Date != "")
            {
                Query += @" AND (
    (DATE(`enter_date`) BETWEEN '" + Date + @"  00:00:00' AND '" + Date + @"  23:59:59') OR
    (DATE(`quit_date`) BETWEEN '" + Date + @"  00:00:00' AND '" + Date + @"  23:59:59')
)";
            }

            // Выберем диапазон дат
            if (DateRangeBegin != "")
            {
                Query += @" AND (
    (DATE(`enter_date`) BETWEEN '" + DateRangeBegin + @"  00:00:00' AND '" + DateRangeEnd + @"  23:59:59') OR
    (DATE(`quit_date`) BETWEEN '" + DateRangeBegin + @"  00:00:00' AND '" + DateRangeEnd + @"  23:59:59')
)";
            }

            // Выберем причину выхода из системы. Зачем может понадобиться – не знаю.
            if (Reason != "")
            {
                Query += " AND (`reason_quit` LIKE '%" + Reason + "%')";
            }

            // Выберем IP адрес
            if (IP != "")
            {
                Query += " AND (`ip` LIKE '%" + IP + "%')";
            }

            // Выберем активных пользователей.
            if (Active != "")
            {
                Query += " AND (`quit_date` IS NULL)";
            }

            // Добавим обратную сортировку и лимит
            Query += " ORDER BY `id` DESC LIMIT " + Limit + ";";

            DataTable Res = DataBase.Query(Query);

            // И отошлём всё.
            List<string> Out = new List<string>();
            Out.Add("| ID\t | Start date   time  \t | End   date   time  \t | User            | IP              | Reason");
            Out.Add("|--------|-----------------------|-----------------------|-----------------|-----------------|-----------------------------------");

            //Server Fail – quit date of restart
            if (Res.Rows.Count == 0) Out.Add("Results not found");

            for (int i = 0; i < Res.Rows.Count; i++)
            {
                string msg = "| " + Res.Rows[i].ItemArray[0].ToString() + "\t | ";
                msg += Res.Rows[i].ItemArray[1].ToString().Replace("\n", "").Replace("\r", "") + "\t | ";
                msg += Res.Rows[i].ItemArray[2].ToString() != ""
                    ? Res.Rows[i].ItemArray[2].ToString() + "\t | "
                    : "---------- --:--:--\t | ";
                msg += Res.Rows[i].ItemArray[3].ToString() +
                    new String(' ', 15 - Res.Rows[i].ItemArray[3].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[4].ToString() +
                    new String(' ', 15 - Res.Rows[i].ItemArray[4].ToString().Length) + " | ";
                msg += Res.Rows[i].ItemArray[5].ToString() + "\t";
                Out.Add(msg);
            }
            CurUser.Transport.SimpleMsg(handler, Out);
        }

        /// <summary>
        /// Заносит в журнал команду и выдаёт ID записи
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="DataBase"></param>
        /// <param name="CurUser"></param>
        /// <param name="Command"></param>
        /// <param name="Params"></param>
        /// <returns></returns>
        public int SaveQuery(Socket handler, User CurUser, string Command, string Params)
        {
            string LogQuery = Command.Trim() != Account.LoginAll
                        ? @"INSERT INTO `queries` (`user`, `session`,`ip`,`command`,`parameters`) 
                            VALUES (" + CurUser.GetID().ToString() + ", " + CurUser.GetSessionID().ToString() +
                            ", '" + ((IPEndPoint)handler.RemoteEndPoint).Address.ToString() + "', '" + Command +
                            "', '" + Params + "');"
                        : @"INSERT INTO `queries` (`ip`,`command`,`parameters`) 
                            VALUES ('" + ((IPEndPoint)handler.RemoteEndPoint).Address.ToString() + "', '" + Command +
                            "', '" + Params + "');";
            DataBase.ExecuteQuery(LogQuery);
            return DataBase.GetLastID();
        }

        /// <summary>
        /// Добавляет комментарий к строке в журнале
        /// </summary>
        /// <param name="LogID">Номер строки</param>
        /// <param name="Comment">Текст комментария</param>
        public void AddToQueryLog(int LogID, string Comment)
        {
            DataBase.ExecuteQuery("UPDATE `queries` SET `comment` = '" + Comment + "' " +
                                        "WHERE `id` = " + LogID.ToString() + ";");
        }

        /// <summary>
        /// Добавляет комментарий к строке в журнале
        /// </summary>
        /// <param name="DataBase">БД</param>
        /// <param name="LogID">Номер строки</param>
        /// <param name="Comment">Текст комментария</param>
        public static void AddToQueryLog(DB DataBase, int LogID, string Comment)
        {
            DataBase.ExecuteQuery("UPDATE `queries` SET `comment` = '" + Comment + "' " +
                                        "WHERE `id` = " + LogID.ToString() + ";");
        }
    }
}
