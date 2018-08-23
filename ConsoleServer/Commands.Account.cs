using ConsoleServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Commands
{
    class Account : ExecutableCommand, IUserListCommand
    {
        // Команды по молекулам
        public const string Help = "help";                  // Подсказка
        public const string Login = "login";                // Войти
        public const string LoginAll = "account." + Login;  // Полная команда на вход
        public const string Quit = "quit";                  // Выйти
        public const string SetPassword = "set_password";   // Сменить пароль

        public Account(DB dataBase) : base(dataBase)
        {
            Name = "account";
        }

        public void Execute(Socket handler, User CurUser, 
            string[] Command, string[] Params, List<User> Active_Users, int LogID)
        {
            if (Command.Length == 1)
            {
                SendHelp(handler);
                return;
            }

            switch (Command[1].ToLower())
            {
                case Help: SendHelp(handler); break;
                case Login: LogIn(handler, Active_Users, Params, LogID); break;
                case Quit: UserQuit(handler, CurUser, Active_Users); break;
                case SetPassword: /*SearchMoleculesBySMILES(handler, CurUser, DataBase, Params); */break;
                default: SimpleMsg(handler, "Unknown command"); break;
            }
        }

        private void UserQuit(Socket handler, User CurUser, List<User> Active_Users)
        {
            CurUser.Quit("User Quited");
            Active_Users.Remove(CurUser);
            SimpleMsg(handler, "OK");
        }

        private void LogIn(Socket handler, List<User> Active_Users, string[] Params,
            int LogID)
        {
            string UserName = "";
            string Password = "";

            foreach (string Param in Params)
            {
                string[] Parameter = Param.Split(' ');
                switch (Parameter[0].ToLower())
                {
                    case "name":
                        UserName = Param.Remove(0, 5); break;
                    case "password":
                        Password = Param.Remove(0, 9); break;
                    default: break;
                }
            }

            // Найдём уже залогиненных пользователей с таким же ником
            List<User> UserList = Active_Users.FindAll(x => x.GetLogin() == UserName);
            foreach (User x in UserList)       // И всех их «выйдем»
            {
                x.Quit("User Relogin");
            }

            // ...удалив из списка
            if (UserList != null)
            {
                Active_Users.RemoveAll(x => x.GetLogin() == UserName);
            };

            // Залогинемся. Здесь происходит обращение к БД. В случае ошибки UserID будет User.NoUserID
            User NewUser = new User(UserName, Password, DataBase,
                ((IPEndPoint)handler.RemoteEndPoint).Address.ToString());

            // Если такой пользователь есть, то добавим его в список
            if (NewUser.GetUserID() != User.NoUserID)
            { Active_Users.Add(NewUser); }
            else   // Если нет, то напишем об этом в журнал
            {
                DataBase.ExecuteQuery("UPDATE `queries` SET `comment` = '! User name and/or pasword invalid' " +
                                    "WHERE `id` = " + LogID.ToString() + ";");
            }

            SendMsg(handler, Answer.StartMsg);
            SendMsg(handler, Answer.LoginOK);
            SendMsg(handler, NewUser.GetUserID());
            SendMsg(handler, NewUser.GetID().ToString());
            SendMsg(handler, NewUser.GetName());
            SendMsg(handler, NewUser.GetFathersName());
            SendMsg(handler, NewUser.GetSurname());
            SendMsg(handler, NewUser.Status());
            SendMsg(handler, Answer.EndMsg);
        }

        private void SendHelp(Socket handler)
        {
            SimpleMsg(handler, @"Command for log in system. Possible comands:
 - account.login - Log in system
 - account.quit - Log out system
 - account.set_password - Change YOUR password. To change somebody's password use 'users' command");
        }
    }
}
