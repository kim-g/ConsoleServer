using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Commands
{

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

    class Laboratories
    {
        // Команды по лабораториям
        public const string Help    = "laboratories";          // справка по командам laboratories
        public const string Add     = "laboratories.add";      // Добавление новой лаборатории
        public const string Edit    = "laboratories.update";   // Изменение лаборатории
        public const string List    = "laboratories.list";     // Вывод лабораторий на консоль
        public const string Names   = "laboratories.names";    // Вывод ID и имён лабораторий 
    }
}
