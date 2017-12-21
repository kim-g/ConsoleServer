using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using ConsoleServer;
using System.Data;

namespace Commands
{

    class ExecutableCommand
    {
        // Посылает сообщение с открывающей и закрывающей командами
        public static void SimpleMsg(Socket handler, string Message)
        {
            SendMsg(handler, Answer.StartMsg);
            SendMsg(handler, Message);
            SendMsg(handler, Answer.EndMsg);
        }

        // Очищает список параметров от мусора и соединяет в одну строку
        public static string AllParam(string[] Param)
        {
            string Text = Param[1].Replace("\n", "").Replace("\r", "");
            for (int j = 2; j < Param.Count(); j++)
            {
                string ClearParam = Param[j].Replace("\n", "").Replace("\r", "");
                if (ClearParam == "") continue;
                Text += " " + ClearParam;
            }
            return Text;
        }

        // Выдаёт единственный параметр
        public static string SimpleParam(string[] Param)
        {
            return Param[1].Replace("\n", "").Replace("\r", "");
        }

        // Вызывает внешниюю команду посылки сообщения
        public static void SendMsg(Socket handler, string Msg)
        {
            SocketServer.Program.SendMsg(handler, Msg);
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

    

    class Molecules
    {
        // Команды по молекулам
        public const string Help = "molecules";             // справка по командам molecules
        public const string Add = "molecules.add";          // Добавление молекулы
        public const string Search = "molecule.search";     // Поиск по молекулам
    }

    
}
