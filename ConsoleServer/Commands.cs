using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using ConsoleServer;

namespace Commands
{

    class ExecutableCommand
    {
        public static void SimpleMsg(Socket handler, string Message)
        {
            SocketServer.Program.SendMsg(handler, Answer.StartMsg);
            SocketServer.Program.SendMsg(handler, Message);
            SocketServer.Program.SendMsg(handler, Answer.EndMsg);
        }

        public static string AllParam(string[] Param)
        {
            string Text = Param[1].Replace("\n", "").Replace("\r", "");
            for (int j = 2; j < Param.Count(); j++)
                Text += " " + Param[j].Replace("\n", "").Replace("\r", "");
            return Text;
        }

        public static string SimpleParam(string[] Param)
        {
            return Param[1].Replace("\n", "").Replace("\r", "");
        }
    }

    class Global
    {
        //Команды от клиента в нулевой строке (Часть устаревшие. Сохранены для совместимости.)
        public const string Search_Mol = "<@Search_Molecule@>";
        public const string Add_User = "<@Add_User@>";
        public const string Add_Mol = "<@Add_Molecule@>";
        public const string Login = "<@Login_User@>";
        public const string Status = "<@Next_Status@>";
        public const string GetStatuses = "<@Get_Status_List@>";
        public const string QuitMsg = "<@*Quit*@>";
        public const string FN_msg = "<@GetFileName@>";
        public const string Show_My_mol = "<@Show my molecules@>";  // Команда показать все молекулы
        public const string Increase_Status = "<@Increase status@>"; // Увеличеть значение статуса соединения
        public const string Show_New_Mol = "<@Show new molecules@>";  // Команда показать все молекулы новые
        public const string SendFileMsg = "<@*Send_File*@>";
        public const string GetFileMsg = "<@*Get_File*@>";
        public const string All_Users = "<@Show_All_Users@>";
        public const string ShowHash = "<@Show_Hash@>";

        public const string Help = "help";      // Справка по консоли администратора
    }

    class Answer
    {
        // Ответные команды
        public const string LoginOK = "<@Login_OK@>";
        public const string LoginExp = "<@Login_Expired@>";
        public const string StartMsg = "<@Begin_Of_Session@>";
        public const string EndMsg = "<@End_Of_Session@>";
        public const string Answer_Admin = "AdminOK";
        public const string Answer_Manager = "ManagerOK";
    }

    class Database
    {
        public const string Help = "database";    // Справка по использованию БД
        public const string LastID = "database.show_last_id";    // Показать последний использованный ID
    }

    class Log
    {
        // СК по журналу
        public const string Help = "log";    // Справка по использованию журнала
        public const string Session = "log.sessions";    // Показать список сессий
        public const string Query = "log.queries";    // Показать список запросов.
    }

    class Users
    {
        // СК по пользователям
        public const string Help = "users";   // Справка по командам со списком пользователей.
        public const string List = "users.list";  // Вывод всех пользователей
        public const string ActiveUsersList = "users.active";  // Вывод залогиненных пользователей
        public const string Add = "users.add";  // Добавление нового пользователя через консоль
        public const string Update = "users.update";  // Изменение данных пользователя через консоль
        public const string Remove = "users.remove";  // Скрытие пользователя и запрет ему на работу (запись не удаляется)
        public const string RMRF = "users.rmrf";  // Удаление пользователя из БД.
    }

    class Molecules
    {
        // Команды по молекулам
        public const string Help = "molecules";             // справка по командам molecules
        public const string Add = "molecules.add";          // Добавление молекулы
        public const string Search = "molecule.search";     // Поиск по молекулам
    }

    class Laboratories: ExecutableCommand
    {
        // Команды по лабораториям
        public const string Name    = "laboratories";           // Название корневой команды
        public const string Help    = "help";                   // справка по командам laboratories
        public const string Add     = "add";                    // Добавление новой лаборатории
        public const string Edit    = "update";                 // Изменение лаборатории
        public const string List    = "list";                   // Вывод лабораторий на консоль
        public const string Names   = "names";                  // Вывод ID и имён лабораторий 

        public static void Execute(Socket handler, User CurUser, DB DataBase, string [] Command, string [] Params)
        {
            if (Command.Length == 1)
            {
                SendHelp(handler);
                return;
            } 

            switch (Command[1])
            {
                case Help: SendHelp(handler); break;
                case Add: AddLaboratory(handler, CurUser, DataBase, Params); break;
                case Edit: UpdateLaboratory(handler, CurUser, DataBase, Params); break;
            }
        }

        private static void SendHelp(Socket handler)
        {
            SimpleMsg(handler, @"List of laboratories. Helps to manage it. Possible comands:
 - users.list - shows all users;
 - users.add - Adds new laboratory
 - users.update - changes laboratory information
 - users.names - Shows list of IDs and Names of laboratories.");
        }

        private static void AddLaboratory(Socket handler, User CurUser, DB DataBase, string[] Params)
        {
            // Если не админ и не менеджер, то ничего не покажем!
            if (!CurUser.GetUserAddRermissions())
            {
                SimpleMsg(handler, "Access denied");
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
                    SimpleMsg(handler, @"Command to add new Laboratory. Please, enter all information about the Laboratory. Parameters must include:
 - name [Name] - laboratory's full name
 - abb [ABB] - laboratory's abbriviation.");
                    return;
                }
            }

            // Проверяем данные
            if (LName == "")
            {
                SimpleMsg(handler, "Enter name of laboratory");
                return;
            }
            if (LAbbr == "")
            {
                SimpleMsg(handler, "Enter abbrivation of laboratory");
                return;
            }

            // Добавляем в базу
            DataBase.ExecuteQuery("INSERT INTO `laboratory` (`name`, `abbr`) VALUES  ('" + 
                LName + "','" + LAbbr + "')");
            SimpleMsg(handler, "Laboratory added successfully");
        }

        private static void UpdateLaboratory(Socket handler, User CurUser, DB DataBase, string[] Params)
        {
            // Если не админ и не менеджер, то ничего не покажем!
            if (!CurUser.GetUserAddRermissions())
            {
                SimpleMsg(handler, "Access denied");
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
                    SimpleMsg(handler, @"Command to add new Laboratory. Please, enter all information about the Laboratory. Parameters must include:
 - name [Name] - laboratory's full name
 - abb [ABB] - laboratory's abbriviation.");
                    return;
                }
            }

            // Проверяем данные
            if (ID == "")
            {
                SimpleMsg(handler, "No ID entered");
                return;
            }
            if (DataBase.RecordsCount("laboratory","`id`=" + ID) == 0)
            {
                SimpleMsg(handler, "Laboratory with ID = " + ID + " was not found");
                return;
            }
            if (LName == "" && LAbbr == "")
            {
                SimpleMsg(handler, "Error: Nothing to change");
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
            SimpleMsg(handler, "Laboratory updated successfully");
        }
    }
}
