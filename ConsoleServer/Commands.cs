using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using ConsoleServer;
using System.Data;

namespace Commands
{
    /// <summary>
    /// Абстрактный класс, позволяющий обрабатывать блоки команд
    /// </summary>
    abstract class ExecutableCommand
    {
        public DB DataBase { get; set; }
        public string Name;               // Название
        public ExecutableCommand(DB dataBase)
        {
            DataBase = dataBase;
        }

        /// <summary>
        /// Очищает список параметров от мусора и соединяет в одну строку
        /// </summary>
        /// <param name="Param">Параметры</param>
        /// <returns></returns>
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

        /// <summary>
        /// Выдаёт единственный параметр
        /// </summary>
        /// <param name="Param">Параметры</param>
        /// <returns></returns>
        public static string SimpleParam(string[] Param)
        {
            return Param[1].Replace("\n", "").Replace("\r", "");
        }

        /// <summary>
        /// Вызывает внешниюю команду посылки сообщения
        /// </summary>
        /// <param name="handler">Сокет, через который отправляется сообщение</param>
        /// <param name="Msg">Текст сообщения</param>
        public static void SendMsg(Socket handler, string Msg)
        {
            Program.SendMsg(handler, Msg);
        }

        /// <summary>
        /// Выдаёт строку с пробелами в конце до нужного значения. 
        /// Если строка превышает объём, то она НЕ обрезается.
        /// </summary> 
        /// <param name="Text">Строка для обработки</param>
        /// <param name="Length">Минимальная длина выходного текста</param>
        public static string StringLength(string Text, int Length)
        {
            if (Text.Length >= Length) return Text;
            return Text + new String(' ', Length - Text.Length);
        }

        /// <summary>
        /// Поиск элементов в БД
        /// </summary>
        /// <param name="Query"></param>
        /// <returns></returns>
        protected List<string> GetRows(string Query)
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

        protected string NotNull(string Text) => Text != "" ? Text : "<@None@>";

        protected string NotNullSQL(string Text) => Text != "" ? Text : "NULL";
    }

    interface IStandartCommand
    {
        /// <summary>
        /// Выполняет поиск подкоманды и обеспечивает её реализацию
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Command"></param>
        /// <param name="Params"></param>
        void Execute(Socket handler, User CurUser, string[] Command, string[] Params);
    }

    interface IUserListCommand
    {
        /// <summary>
        /// Выполняет поиск подкоманды и обеспечивает её реализацию
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Command"></param>
        /// <param name="Params"></param>
        void Execute(Socket handler, User CurUser, string[] Command,
            string[] Params, List<User> ActiveUsers, int LogID);
    }

    interface ILogCommand
    {
        /// <summary>
        /// Выполняет поиск подкоманды и обеспечивает её реализацию
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="Command"></param>
        /// <param name="Params"></param>
        void Execute(Socket handler, User CurUser, string[] Command,
            string[] Params, int LogID);
    }

    class Answer
    {
        // Ответные команды
        public const string LoginOK = "<@Login_OK@>";
        public const string LoginExp = "<@Login_Expired@>";
        public const string StartMsg = "<@Begin_Of_Session@>";
        public const string EndMsg = "<@End_Of_Session@>";
    }




    

    

    
}
