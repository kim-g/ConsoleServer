using ConsoleServer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Commands
{
    class Users : ExecutableCommand, IStandartCommand
    {
        // СК по пользователям
        public const string Help = "help";             // Справка по командам со списком пользователей.
        public const string List = "list";  // Вывод всех пользователей
        public const string ActiveUsersList = "active";  // Вывод залогиненных пользователей
        public const string Add = "add";  // Добавление нового пользователя через консоль
        public const string Update = "update";  // Изменение данных пользователя через консоль
        public const string Remove = "remove";  // Скрытие пользователя и запрет ему на работу (запись не удаляется)
        public const string RMRF = "rmrf";  // Удаление пользователя из БД.

        public Users(DB dataBase) : base(dataBase)
        {
            Name = "users";           // Название корневой команды
        }

        /// <summary>
        /// Реализация подкоманд
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Command"></param>
        /// <param name="Params"></param>
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
                case List: ShowUsersList(handler, CurUser, Params); break;
                case ActiveUsersList: ShowActiveUsersList(handler, CurUser, Params); break;
                case Add: AddUser(handler, CurUser, Params); break;
                case Update: UpdateUser(handler, CurUser, Params); break;
                case Remove: RemoveUser(handler, CurUser, Params); break;
                case RMRF: RMRFUser(handler, CurUser, Params); break;
                default: CurUser.Transport.SimpleMsg(handler, "Unknown command"); break;
            }
        }

        private void SendHelp(Socket handler, User CurUser)
        {
            CurUser.Transport.SimpleMsg(handler, @"List of users. Helps to manage user list. Possible comands:
 - users.list - shows all users;
 - users.active - shows currently logged in users;
 - users.add - Adds new user
 - users.update - changes user information
 - users.remove - delete user. May be reversible. Safe for work;
 - users.rmrf - delete the user's record. Irreversable.");
        }

        /// <summary>
        /// Показать список пользователей
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Params"></param>
        private void ShowUsersList(Socket handler, User CurUser, string[] Params)
        {
            // Взять всё из журнала и...
            string Query = @"SELECT `persons`.`id`, `Surname`, `persons`.`name`, `fathers_name`, `laboratory`.`abbr`, `job`, `Permissions`, `login`  
FROM `persons` 
INNER JOIN `laboratory` ON (`laboratory`.`id` = `persons`.`laboratory`)";

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string ID = "";
            string Surname = "";
            string Laboratory = "";
            string Permissions = "";
            string Login = "";
            string Limit = "100";

            // Посмотрим все доп. параметры
            for (int i = 0; i < Params.Count(); i++)
            {
                string[] Param = Params[i].ToLower().Split(' '); // Доп. параметр от значения отделяется пробелом
                if (Param[0] == "id") ID = SimpleParam(Param);                    // Показать пользователей по ID
                if (Param[0] == "surname") Surname = SimpleParam(Param);          // Показать пользователей по фамилии
                if (Param[0] == "laboratory") Laboratory = SimpleParam(Param);    // Показать пользователей по лаборатории
                if (Param[0] == "permissions") Permissions = SimpleParam(Param);  // Показать пользователей по правам
                if (Param[0] == "login") Login = SimpleParam(Param);              // Показать пользователей по логину
                if (Param[0] == "limit") Limit = SimpleParam(Param);              // Показать конкретное число пользователей

                // Служебные
                if (Param[0] == "help")     // Помощь
                {
                    CurUser.Transport.SimpleMsg(handler, @"user.list shows list of all users on the server. There are several filter parameters:

 - id [Number] - Show person with ID = Number;
 - surname [Name] - Show persons with surname containings [Name];
 - laboratory [ABB] - Shows persons of this laboratory;
 - permissions [Number] - Shows persons with certain permissions;
 - limit [Number] - How many persons to show. Default is 100;
 - login [Name] - Show persons with login containings [Name];

Parameters may be combined.");
                    return;
                }
            }

            // WHERE будет всегда – показываем только неудалённых
            Query += "\nWHERE (`active` = 1)";

            //Выберем по фамилии
            if (ID != "") Query += " AND (`id` = " + ID + ")";

            //Выберем по фамилии
            if (Surname != "") Query += " AND (`Surname` LIKE '%" + Surname + "%')";

            //Выберем по лаборатории
            if (Laboratory != "") Query += " AND (`laboratory`.`abbr` LIKE '%" + Laboratory + "%')";

            //Выберем по разрешениям
            if (Permissions != "") Query += " AND (`permissions` = " + Permissions + ")";

            //Выберем по логину
            if (Login != "") Query += " AND (`login` LIKE '%" + Login + "%')";

            // Покажем только тех, кого можно конкретному пользователю
            Query += " AND (" + CurUser.GetUserListRermissions() + ")";


            // Добавим обратную сортировку и лимит
            Query += "\nORDER BY `Surname`\nLIMIT " + Limit + ";";

            DataTable Res = DataBase.Query(Query);

            // И пошлём всё пользователю.
            List<string> Out = new List<string>();
            Out.Add("| ID     | Surname              | Name                 | Second Name          | login           | Lab.  | Job        | Perm. |");
            Out.Add("|--------|----------------------|----------------------|----------------------|-----------------|-------|------------|-------|");

            //Server Fail – quit date of restart
            if (Res.Rows.Count == 0) SendMsg(handler, "Results not found");

            for (int i = 0; i < Res.Rows.Count; i++)
            {
                string msg = "| " + Res.Rows[i].ItemArray[0].ToString() + "\t | ";
                msg += StringLength(Res.Rows[i].ItemArray[1].ToString(), 20) + " | ";
                msg += StringLength(Res.Rows[i].ItemArray[2].ToString(), 20) + " | ";
                msg += StringLength(Res.Rows[i].ItemArray[3].ToString(), 20) + " | ";
                msg += StringLength(Res.Rows[i].ItemArray[7].ToString(), 15) + " | ";
                msg += StringLength(Res.Rows[i].ItemArray[4].ToString(), 05) + " | ";
                msg += StringLength(Res.Rows[i].ItemArray[5].ToString(), 10) + " | ";
                msg += StringLength(Res.Rows[i].ItemArray[6].ToString(), 05) + " | ";
                Out.Add(msg);
            }
            CurUser.Transport.SimpleMsg(handler, Out);
        }

        /// <summary>
        ///  Показать список активных пользователей
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Params"></param>
        private void ShowActiveUsersList(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ, то ничего не покажем!
            if (!CurUser.IsAdmin())
            {
                CurUser.Transport.SimpleMsg(handler, "Access denied! You should have admin privelegies");
                return;
            }

            List<string> Out = new List<string>();
            foreach (User U in ConsoleServer.Program.Active_Users)
            {
                string msg = "| " + StringLength(U.GetID().ToString(), 05) + " | ";
                msg += StringLength(U.GetSurname(), 20) + " | ";
                msg += StringLength(U.GetName(), 20) + " | ";
                msg += StringLength(U.GetFathersName(), 20) + " | ";
                msg += StringLength(U.GetLogin(), 15) + " | ";
                string Lab = DataBase.Query("SELECT `abbr` FROM `laboratory` WHERE `id` = " +
                    U.GetLaboratory().ToString() + " LIMIT 1;").Rows[0].ItemArray[0].ToString();
                msg += StringLength(Lab, 5) + " | ";
                msg += StringLength(U.GetJob(), 10) + " | ";
                msg += StringLength(U.GetPermissionsInt().ToString(), 5) + " | ";
                msg += StringLength(U.GetUserID().ToString(), 20) + " | ";

                Out.Add(msg);
            }
            CurUser.Transport.SimpleMsg(handler, Out);
        }

        /// <summary>
        ///  Добавить нового пользователя через командную строку
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Params"></param>
        private void AddUser(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ и не менеджер, то ничего не покажем!
            if (!CurUser.GetUserAddRermissions())
            {
                CurUser.Transport.SimpleMsg(handler, "Access denied");
                return;
            }

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string Name = "";
            string FName = "";
            string Surname = "";
            string LoginN = "";
            string Password = "";
            string CPassword = "";
            string Permissions = "";
            string Laboratory = "";
            string LaboratoryID = "";
            string Job = "";

            // Ищем данные
            foreach (string Line in Params)
            {
                string[] Param = Line.Split(' ');

                switch (Param[0])
                {
                    case "name": Name = AllParam(Param); break;
                    case "second_name": FName = AllParam(Param); break;
                    case "surname": Surname = AllParam(Param); break;
                    case "login": LoginN = SimpleParam(Param); break;
                    case "password": Password = AllParam(Param); break;
                    case "confirm": CPassword = AllParam(Param); break;
                    case "permissions": Permissions = SimpleParam(Param); break;
                    case "laboratory": Laboratory = SimpleParam(Param); break;
                    case "laboratory_id": LaboratoryID = SimpleParam(Param); break;
                    case "job": Job = AllParam(Param); break;
                    case "help": CurUser.Transport.SimpleMsg(handler, @"Command to add new user. Please, enter all information about the user. Parameters must include:
 - name [Name] - person's first name
 - second_name [Name] - person's second name. May be empty.
 - surname [Name] - person's surname.
 - login [Name] - person's login/ Must be unique.
 - password [Phrase] - person's password.
 - confirm [Phrase] - password confirmation. Must be the same as password.
 - permissions [Number]. 
   - 0 - Able to add and get only his/her own molecules
   - 1 - Able to add only his/her own molecules and to get all laboratory's molecules
   - 2 - Able to add and get all laboratory's molecules
   - 3 - Able to add only his/her own molecules and to get all institute's molecules
   - 4 - Able to add all laboratory's molecules and to get all institute's molecules
   - 5 - Able to add and get all institute's molecules
   - 10 - Administrator's right. Able to add and get all institute's molecules. Able to rewind queries statuses. Able to work with user list. Able to direct work with database. Able to work with console.
   - 11 - Manager. Able to add and get all institute's molecules. Able to rewind queries statuses.
 - laboratory [Abbr.] - laboratory's abbreviation.
or
 - laboratory_id [id] - laboratory's ID in DB.
 - job [Name] - person's job."); break;
                }
            }


            // Проверяем, все ли нужные данные есть
            if (Name == "") { CurUser.Transport.SimpleMsg(handler, "Error: No name entered"); return; }
            if (Surname == "") { CurUser.Transport.SimpleMsg(handler, "Error: No surname entered"); return; }
            if (LoginN == "") { CurUser.Transport.SimpleMsg(handler, "Error: No login entered"); return; }
            if (Password == "") { CurUser.Transport.SimpleMsg(handler, "Error: No password entered"); return; }
            if (CPassword == "") { CurUser.Transport.SimpleMsg(handler, "Error: No password conformation entered"); return; }
            if (Permissions == "") { CurUser.Transport.SimpleMsg(handler, "Error: No permissions entered"); return; }
            if (Laboratory == "" && LaboratoryID == "") { CurUser.Transport.SimpleMsg(handler, "Error: No laboratory number entered"); return; }

            if (LaboratoryID != "")
            {
                if (DataBase.RecordsCount("laboratory", "id=" + LaboratoryID) == 0)
                { CurUser.Transport.SimpleMsg(handler, "Error: Laboratory not found"); return; }
            }
            if (Laboratory != "")
            {
                DataTable DT = DataBase.Query("SELECT `id` FROM `laboratory` WHERE `abbr`='" + Laboratory + "' LIMIT 1;");
                if (DT.Rows.Count == 0) { CurUser.Transport.SimpleMsg(handler, "Error: Laboratory not found"); return; };
                LaboratoryID = DT.Rows[0].ItemArray[0].ToString();
            }

            // Проверка корректности введённых данных
            if (DataBase.RecordsCount("persons", "`login`='" + LoginN + "'") > 0)
            { CurUser.Transport.SimpleMsg(handler, "Error: Login exists"); return; };
            if (Password != CPassword)
            { CurUser.Transport.SimpleMsg(handler, "Error: \"password\" and \"confirm\" should be the similar"); return; }

            // Добавление пользователя в БД
            new User(LoginN, Password, Name, FName, Surname, Convert.ToInt32(Permissions), LaboratoryID, Job,
                DataBase);
            CurUser.Transport.SimpleMsg(handler, "User added");          
        }

        /// <summary>
        ///  Изменить данные пользователя через командную строку
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Params"></param>
        private void UpdateUser(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ и не менеджер, то ничего не покажем!
            if (!CurUser.GetUserAddRermissions())
            {
                CurUser.Transport.SimpleMsg(handler, "Access denied");
                return;
            }

            // Начальная инициация переменных, чтобы из IF(){} вышли
            string ULogin = "";
            string Name = "";
            string FName = "";
            string Surname = "";
            string LoginN = "";
            string Password = "";
            string CPassword = "";
            string OldPassword = "";
            string Permissions = "";
            string Laboratory = "";
            string Job = "";

            // Ищем данные
            foreach (string Line in Params)
            {
                string[] Param = Line.Split(' ');

                if (Param[0] == "login") ULogin = SimpleParam(Param);
                if (Param[0] == "name") Name = SimpleParam(Param);
                if (Param[0] == "second.name") FName = SimpleParam(Param);
                if (Param[0] == "surname") Surname = SimpleParam(Param);
                if (Param[0] == "newlogin") LoginN = SimpleParam(Param);
                if (Param[0] == "oldpassword") OldPassword = SimpleParam(Param);
                if (Param[0] == "password") Password = SimpleParam(Param);
                if (Param[0] == "confirm") CPassword = SimpleParam(Param);
                if (Param[0] == "permissions") Permissions = SimpleParam(Param);
                if (Param[0] == "laboratory") Laboratory = SimpleParam(Param);
                if (Param[0] == "job") Job = SimpleParam(Param);

                // Помощь
                if (Param[0] == "help")
                {
                    CurUser.Transport.SimpleMsg(handler, @"Command to update user's information. Parameters may include:
 - login [login] - login of user to change
 - name [Name] - person's first name
 - second.name [Name] - person's second name. May be empty.
 - surname [Name] - person's surname.
 - newlogin [login] - person's new login/ Must be unique.
 - oldpassword [Phrase] - person's existing password. Nessessary to change password.
 - password [Phrase] - person's password.
 - confirm [Phrase] - password confirmation. Must be the same as password.
 - permissions [Number]. 
   - 0 - Able to add and get only his/her own molecules
   - 1 - Able to add only his/her own molecules and to get all laboratory's molecules
   - 2 - Able to add and get all laboratory's molecules
   - 3 - Able to add only his/her own molecules and to get all institute's molecules
   - 4 - Able to add all laboratory's molecules and to get all institute's molecules
   - 5 - Able to add and get all institute's molecules
   - 10 - Administrator's right. Able to add and get all institute's molecules. Able to rewind queries statuses. Able to work with user list. Able to direct work with database. Able to work with console.
   - 11 - Manager. Able to add and get all institute's molecules. Able to rewind queries statuses.
 - laboratory [Abbr.] - laboratory's abbreviation.
 - job [Name] - person's job.");
                    return;
                }

            }

            // Проверяем, все ли нужные данные есть
            if (ULogin == "") { CurUser.Transport.SimpleMsg(handler, "Error: No login of user to change information. Use \"login\" parameter."); return; }
            int LabNum = -1;
            if (Laboratory != "")
            {
                DataTable DT = DataBase.Query("SELECT `id` FROM `laboratory` WHERE `abbr`='" + Laboratory + "' LIMIT 1;");
                if (DT.Rows.Count == 0) { CurUser.Transport.SimpleMsg(handler, "Error: Laboratory not found"); return; };
                LabNum = (int)DT.Rows[0].ItemArray[0];
            }

            // Проверка корректности введённых данных
            if (LoginN != "")
                if (DataBase.RecordsCount("persons", "`login`='" + LoginN + "'") > 0)
                { CurUser.Transport.SimpleMsg(handler, "Error: Login exists"); return; };
            if (Password != "")
                if (Password != CPassword)
                { CurUser.Transport.SimpleMsg(handler, "Error: \"password\" and \"confirm\" should be the similar"); return; }
            if (LabNum != -1)
                if (DataBase.RecordsCount("laboratory", "`id`=" + LabNum.ToString() + "") == 0)
                { CurUser.Transport.SimpleMsg(handler, "Error: Laboratory not found"); return; };
            List<string> id_list = DataBase.QueryOne("SELECT `id` FROM `persons` WHERE `login` = '" + ULogin + "' LIMIT 1;");
            if (id_list == null) { CurUser.Transport.SimpleMsg(handler, "Error: No such login of user to change information."); return; }

            // Открытие записи пользователя в БД
            User UserToChange = new User(id_list[0], DataBase);

            //Испраляем всё то, что нам послали.
            if (Name != "")
                if (!UserToChange.SetName(Name))
                { CurUser.Transport.SimpleMsg(handler, "Error: Unable to change name."); return; };
            if (FName != "")
                if (!UserToChange.SetSecondName(FName))
                { CurUser.Transport.SimpleMsg(handler, "Error: Unable to change second name."); return; };
            if (Surname != "")
                if (!UserToChange.SetSurname(Surname))
                { CurUser.Transport.SimpleMsg(handler, "Error: Unable to change surname."); return; };
            if (LoginN != "")
                if (!UserToChange.SetLogin(LoginN))
                { CurUser.Transport.SimpleMsg(handler, "Error: Unable to change login. New login should be uniqe."); return; };
            if (Password != "")
                if (!UserToChange.SetPassword(OldPassword, Password))
                { CurUser.Transport.SimpleMsg(handler, "Error: Unable to change password. Old password may be not valid or some error happend."); return; };
            if (Permissions != "")
                if (!UserToChange.SetRights(Convert.ToInt32(Permissions)))
                { CurUser.Transport.SimpleMsg(handler, "Error: Unable to change permissions."); return; };
            if (Laboratory != "")
                if (!UserToChange.SetLaboratory(LabNum))
                { CurUser.Transport.SimpleMsg(handler, "Error: Unable to change laboratory."); return; };
            if (Job != "")
                if (!UserToChange.SetJob(Job))
                { CurUser.Transport.SimpleMsg(handler, "Error: Unable to change job."); return; };


            // Отсылаем команду, что всё хорошо.
            CurUser.Transport.SimpleMsg(handler, "User information updated");

            /*          
                        /\
                       /o \
                      /o  o\
                      --/\--
                       /O \
                      /    \
                     /o   O \
                    /o   o   \
                    ---/  \---
                      /O  o\
                     /      \
                    /o  O   O\
                   /     o    \
                  /  O      O  \
                  -----|  |-----     _
                       |  |       __/ \
    ____               |  |      /     \_____
 --/    \-----------------------/            \-----------------------------

            */

        }

        /// <summary>
        ///  Делаем пользователя неактивным
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Params"></param>
        private void RemoveUser(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ и не менеджер, то ничего не покажем!
            if (!CurUser.GetUserAddRermissions())
            {
                CurUser.Transport.SimpleMsg(handler, "Access denied");
                return;
            }

            // Ищем данные
            foreach (string Line in Params)
            {
                string[] Param = Line.Split(' ');

                // Объявляем параметры
                string ULogin = "";

                if (Param[0] == "login") ULogin = Param[1].Replace("\n", "").Replace("\r", "");
                // Помощь
                if (Param[0] == "help")
                {
                    Users_Remove_Help(handler, CurUser);
                    return;
                }

                // Проверяем, все ли нужные данные есть
                if (ULogin == "") { Users_Remove_Help(handler, CurUser); return; }
                List<string> id_list = DataBase.QueryOne("SELECT `id` FROM `persons` WHERE `login` = '" + ULogin + "' LIMIT 1;");
                if (id_list == null) { CurUser.Transport.SimpleMsg(handler, "Error: No such login of user to change information."); return; }

                // Ищем пользователя.
                User UserToChange = new User(id_list[0], DataBase);
                UserToChange.SetActive(false);

                // И отошлём информацию, что всё OK
                CurUser.Transport.SimpleMsg(handler, "User was removed successfully");
            }
        }


        private void Users_Remove_Help(Socket handler, User CurUser) => 
            CurUser.Transport.SimpleMsg(handler, 
                @"Command to remove user from the system. Reversible. Safe. Parameters may include:
 - login [login] - login of user to remove.");


        /// <summary>
        ///  Полностью удаляет пользователя из базы. Не рекомендуется
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Params"></param>
        private void RMRFUser(Socket handler, User CurUser, string[] Params)
        {
            // Если не админ, то ничего не покажем!
            if (!CurUser.IsAdmin())
            {
                CurUser.Transport.SimpleMsg(handler, "Access denied");
                return;
            }

            // Объявляем параметры
            string ULogin = "";

            // Ищем данные
            foreach (string Line in Params)
            {
                string[] Param = Line.Split(' ');




                if (Param[0] == "login") ULogin = Param[1].Replace("\n", "").Replace("\r", "");
                // Помощь
                if (Param[0] == "help")
                {
                    Users_Remove_Help(handler, CurUser);
                    return;
                }

            }


            // Проверяем, все ли нужные данные есть 
            if (ULogin == "") { Users_Remove_Help(handler, CurUser); return; }
            List<string> id_list = DataBase.QueryOne("SELECT `id` FROM `persons` WHERE `login` = '" + ULogin + "' LIMIT 1;");
            if (id_list == null) { CurUser.Transport.SimpleMsg(handler, "Error: No such login of user to change information."); return; }

            // Ищем пользователя.
            User UserToChange = new User(id_list[0], DataBase);
            UserToChange.DeleteUser();

            // И отошлём информацию, что всё OK
            CurUser.Transport.SimpleMsg(handler, "User was deleted successfully");
        }
    }
}
