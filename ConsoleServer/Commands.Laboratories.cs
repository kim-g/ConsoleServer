using ConsoleServer;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;

namespace Commands
{
    class Laboratories : ExecutableCommand, IStandartCommand
    {
        // Команды по лабораториям
        public const string Help = "help";                   // справка по командам laboratories
        public const string Add = "add";                     // Добавление новой лаборатории
        public const string Edit = "update";                 // Изменение лаборатории
        public const string List = "list";                   // Вывод лабораторий на консоль
        public const string Names = "names";                 // Вывод ID и имён лабораторий 

        public Laboratories(DB dataBase) : base(dataBase)
        {
            Name = "laboratories";
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
                case Add: AddLaboratory(handler, CurUser, Params); break;
                case Edit: UpdateLaboratory(handler, CurUser, Params); break;
                case List: ListShow(handler, CurUser, Params); break;
                case Names: NamesShow(handler, CurUser, Params); break;
                default: CurUser.Transport.SimpleMsg(handler, "Unknown command"); break;
            }
        }

        private void SendHelp(Socket handler, User CurUser)
        {
            CurUser.Transport.SimpleMsg(handler, @"List of laboratories. Helps to manage it. Possible comands:
 - users.list - shows all users;
 - users.add - Adds new laboratory
 - users.update - changes laboratory information
 - users.names - Shows list of IDs and Names of laboratories.");
        }

        private void AddLaboratory(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ и не менеджер, то ничего не покажем!
            if (!CurUser.GetUserAddRermissions())
            {
                CurUser.Transport.SimpleMsg(handler, "Access denied");
                return;
            }

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string LName = "";
            string LAbbr = "";

            // Ищем данные
            foreach (string Line in Params)
            {
                string[] Param = Line.Split(' ');

                if (Param[0] == "name") LName = AllParam(Param);
                if (Param[0] == "abb") LAbbr = SimpleParam(Param);

                // Помощь
                if (Param[0] == "help")
                {
                    CurUser.Transport.SimpleMsg(handler, @"Command to add new Laboratory. Please, enter all information about the Laboratory. Parameters must include:
 - name [Name] - laboratory's full name
 - abb [ABB] - laboratory's abbriviation.");
                    return;
                }
            }

            // Проверяем данные
            if (LName == "")
            {
                CurUser.Transport.SimpleMsg(handler, "Enter name of laboratory");
                return;
            }
            if (LAbbr == "")
            {
                CurUser.Transport.SimpleMsg(handler, "Enter abbrivation of laboratory");
                return;
            }

            // Добавляем в базу
            DataBase.ExecuteQuery("INSERT INTO `laboratory` (`name`, `abbr`) VALUES  ('" +
                LName + "','" + LAbbr + "')");
            CurUser.Transport.SimpleMsg(handler, "Laboratory added successfully");
        }

        private void UpdateLaboratory(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ и не менеджер, то ничего не покажем!
            if (!CurUser.GetUserAddRermissions())
            {
                CurUser.Transport.SimpleMsg(handler, "Access denied");
                return;
            }

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string ID = "";
            string LName = "";
            string LAbbr = "";

            // Ищем данные
            foreach (string Line in Params)
            {
                string[] Param = Line.Split(' ');

                if (Param[0] == "id") ID = SimpleParam(Param);
                if (Param[0] == "name") LName = AllParam(Param);
                if (Param[0] == "abb") LAbbr = SimpleParam(Param);

                // Помощь
                if (Param[0] == "help")
                {
                    CurUser.Transport.SimpleMsg(handler, @"Command to add new Laboratory. Please, enter all information about the Laboratory. Parameters must include:
 - name [Name] - laboratory's full name
 - abb [ABB] - laboratory's abbriviation.");
                    return;
                }
            }

            // Проверяем данные
            if (ID == "")
            {
                CurUser.Transport.SimpleMsg(handler, "No ID entered");
                return;
            }
            if (DataBase.RecordsCount("laboratory", "`id`=" + ID) == 0)
            {
                CurUser.Transport.SimpleMsg(handler, "Laboratory with ID = " + ID + " was not found");
                return;
            }
            if (LName == "" && LAbbr == "")
            {
                CurUser.Transport.SimpleMsg(handler, "Error: Nothing to change");
                return;
            }

            string Addition = "";
            if (LName != "")
                Addition += " `name`='" + LName + "'";
            if (LAbbr != "")
            {
                if (Addition.Length > 0)
                    Addition += ",";
                Addition += " `abbr`='" + LAbbr + "'";
            }

            // Добавляем в базу
            DataBase.ExecuteQuery("UPDATE `laboratory` SET " + Addition + " WHERE `id`=" + ID + " LIMIT 1;");
            CurUser.Transport.SimpleMsg(handler, "Laboratory updated successfully");
        }

        private void ListShow(Socket handler, User CurUser, string[] Params)
        {
            // Взять всё из журнала и...
            string Query = @"SELECT `id`, `abbr`, `name` FROM `laboratory`";

            // Инициация переменных, чтобы из if(){} нормально вышли
            string ID = "";
            string Abbr = "";
            string L_Name = "";
            string Limit = "100";

            // Посмотрим все доп. параметры
            for (int i = 0; i < Params.Count(); i++)
            {
                string[] Param = Params[i].ToLower().Split(' '); // Доп. параметр от значения отделяется пробелом

                if (Param[0] == "id") ID = SimpleParam(Param);
                if (Param[0] == "abbr") Abbr = SimpleParam(Param);
                if (Param[0] == "name") L_Name = AllParam(Param);
                if (Param[0] == "limit") Limit = SimpleParam(Param);
                // Служебные
                if (Param[0] == "help")     // Помощь
                {
                    CurUser.Transport.SimpleMsg(handler, @"Command to show list of laboratories. You may enter filter. Possible filters:
 - id [Number] - laboratory's ID
 - name [Name] - laboratory's full name
 - abb [ABB] - laboratory's abbriviation
 - limit [Number] - Count of records to show. Default value = 100.");
                    return;
                }
            }

            // Счётчик условий.
            int СountConditions = 0;
            //Выберем по ID
            if (ID != "")
            {
                Query += СountConditions == 0
                    ? " WHERE (`id` = " + ID + ")"
                    : " AND (`id` = " + ID + ")";
                СountConditions++;
            }

            // Выберем имя
            if (L_Name != "")
            {
                Query += СountConditions == 0
                    ? " WHERE (`name` LIKE '%" + L_Name + "%')"
                    : " AND (`name` LIKE '%" + L_Name + "%'";
                СountConditions++;
            }

            // Выберем аббривиатуру
            if (Abbr != "")
            {
                Query += СountConditions == 0
                    ? " WHERE (`abbr` LIKE '%" + Abbr + "%')"
                    : " AND (`abbr` LIKE '%" + Abbr + "%'";
                СountConditions++;
            }

            // Добавим лимит
            Query += " LIMIT " + Limit + ";";

            DataTable Res = DataBase.Query(Query);

            // И отошлём всё.
            List<string> Out = new List<string>();
            Out.Add("| ID\t | Abbr \t |Name");
            Out.Add("|--------|--------|-----------------------");

            //Server Fail – quit date of restart
            if (Res.Rows.Count == 0) Out.Add("Results not found");

            for (int i = 0; i < Res.Rows.Count; i++)
            {
                string msg = "| " + Res.Rows[i].ItemArray[0].ToString() + "\t | ";
                msg += Res.Rows[i].ItemArray[1].ToString() + "\t | ";
                msg += Res.Rows[i].ItemArray[2].ToString();
                Out.Add(msg);
            }
            CurUser.Transport.SimpleMsg(handler, Out);

        }

        private void NamesShow(Socket handler, User CurUser, string[] Params)
        {
            // Запросим список лабораторий
            string Query = @"SELECT `id`, `name` FROM `laboratory`";
            DataTable Res = DataBase.Query(Query);

            // И отошлём их, разделяя ID и имя знаком '='
            List<string> Out = new List<string>();

            // Server Fail – quit date of restart
            if (Res.Rows.Count == 0) Out.Add("Results not found");

            for (int i = 0; i < Res.Rows.Count; i++)
            {
                string msg = Res.Rows[i].ItemArray[0].ToString() + "=";
                msg += Res.Rows[i].ItemArray[1].ToString();
                Out.Add(msg);
            }
            CurUser.Transport.SimpleMsg(handler, Out);
        }
    }
}
