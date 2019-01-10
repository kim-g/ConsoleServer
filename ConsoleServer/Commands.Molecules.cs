using ConsoleServer;
using MySql.Data.MySqlClient;
using OpenBabel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Extentions;

namespace Commands
{
    class Molecules : ExecutableCommand, IStandartCommand
    {
        // Команды по молекулам
        public const string Help = "help";                  // Подсказка
        public const string Add = "add";                    // Добавление молекулы
        public const string Search = "search";              // Поиск по молекулам

        public Molecules(DB dataBase) : base(dataBase)
        {
            Name = "molecules";             // Название
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
                case Add: AddMolecule(handler, CurUser, Params); break;
                case Search: SearchMoleculesBySMILES(handler, CurUser, Params); break;
                default: CurUser.Transport.SimpleMsg(handler, "Unknown command"); break;
            }
        }

        /// <summary>
        /// Добавляет молекулу в базу данных
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="DataBase"></param>
        /// <param name="Params"></param>
        private void AddMolecule(Socket handler, User CurUser, string[] Params)
        {
            // Начальная инициация переменных, чтобы из IF(){} вышли
            string Subst = "";
            string Lab = "";
            string Person = "";
            string Structure = "";
            string PhysState = "";
            string Melt = "";
            string Conditions = "";
            string Properties = "";
            string Mass = "";
            string Solution = "";

            // Ищем данные
            foreach (string Line in Params)
            {
                string[] Param = Line.Split(' ');
                switch (Param[0].ToLower())
                {
                    case "code": Subst = SimpleParam(Param); break;
                    case "laboratory": Lab = SimpleParam(Param); break;
                    case "person": Person = SimpleParam(Param); break;
                    case "structure": Structure = SimpleParam(Param); break;
                    case "phys_state": PhysState = SimpleParam(Param); break;
                    case "melting_point": Melt = SimpleParam(Param); break;
                    case "conditions": Conditions = SimpleParam(Param); break;
                    case "properties": Properties = SimpleParam(Param); break;
                    case "mass": Mass = SimpleParam(Param); break;
                    case "solution": Solution = SimpleParam(Param); break;
                    case "help": CurUser.Transport.SimpleMsg(handler, @"Command to add new molecule. Please, enter all information about the this. Parameters must include:
 - code [Name] - Code of the substance.
 - laboratory [Code] - ID of owner's laboratory.
 - person [Code] - ID of owner.
 - structure [SMILES] - molecular structure in SMILES format.
 - phys_state [Phrase] - physical state (liquid, gas, solid...).
 - melting_point [Temperature] - Temperature of melting point.
 - conditions [Phrase] - storage conditions. 
 - properties [Phrase] - other properties.
 - mass [grammes] - mass of surrendered sample.
 - solution [Phrase] - best solutions."); break;
                }
            }

            // Проверяем, все ли нужные данные есть
            if (Subst == "") { CurUser.Transport.SimpleMsg(handler, "Error: No code entered"); return; }
            if (Lab == "") { CurUser.Transport.SimpleMsg(handler, "Error: No laboratory entered"); return; }
            if (Person == "") { CurUser.Transport.SimpleMsg(handler, "Error: No person entered"); return; }
            if (Structure == "") { CurUser.Transport.SimpleMsg(handler, "Error: No structure entered"); return; }
            if (PhysState == "") { CurUser.Transport.SimpleMsg(handler, "Error: No physical state entered"); return; }
            if (Mass == "") { CurUser.Transport.SimpleMsg(handler, "Error: No mass entered"); return; }
            if (Solution == "") { CurUser.Transport.SimpleMsg(handler, "Error: No solution entered"); return; }

            // Добавление и шифровка
            string queryString = @"INSERT INTO `molecules` 
(`name`, `laboratory`, `person`, `b_structure`, `state`, `melting_point`, `conditions`, `other_properties`, `mass`, `solution`)
VALUES (@Name, @Laboratory, @Person, @Structure, @State, @MeltingPoint, @Conditions, @OtherProperties, @Mass, @Solution);";

            MySqlCommand com = DataBase.MakeCommandObject(queryString);
            com.Parameters.AddWithValue("@Name", Subst);
            com.Parameters.AddWithValue("@Laboratory", Lab);
            com.Parameters.AddWithValue("@Person", Person);
            com.Parameters.AddWithValue("@Structure", Program.CommonAES.EncryptStringToBytes(Structure));
            com.Parameters.AddWithValue("@State", Program.CommonAES.EncryptStringToBytes(PhysState));
            com.Parameters.AddWithValue("@MeltingPoint", Program.CommonAES.EncryptStringToBytes(Melt));
            com.Parameters.AddWithValue("@Conditions", Program.CommonAES.EncryptStringToBytes(Conditions));
            com.Parameters.AddWithValue("@OtherProperties", Program.CommonAES.EncryptStringToBytes(Properties));
            com.Parameters.AddWithValue("@Mass", Program.CommonAES.EncryptStringToBytes(Mass));
            com.Parameters.AddWithValue("@Solution", Program.CommonAES.EncryptStringToBytes(Solution));

            com.ExecuteNonQuery();

            //И отпишемся.
            CurUser.Transport.SimpleMsg(handler, "Add_Molecule: done");
        }

        /// <summary>
        /// Показывает справку о команде
        /// </summary>
        /// <param name="handler"></param>
        private void SendHelp(Socket handler, User CurUser)
        {
            CurUser.Transport.SimpleMsg(handler, @"List of molecules. The main interface to work with molecules. Possible comands:
 - molecules.add - Adds new molecule
 - molecules.search - changes user information");
        }

        /// <summary>
        /// Поиск молекулы по SMILES
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="CurUser"></param>
        /// <param name="DataBase"></param>
        /// <param name="Params"></param>
        private void SearchMoleculesBySMILES(Socket handler, User CurUser, string[] Params)
        {
            string Structure = "";
            string UserID = "";
            string SearchAria = "Permission";
            string Status = "0";

            foreach (string Param in Params)
            {
                string[] Parameter = Param.Split(' ');
                switch (Parameter[0].ToLower())
                {
                    case "structure":
                        Structure = AllParam(Parameter); break;
                    case "user":
                        UserID = SimpleParam(Parameter); break;
                    case "my":
                        SearchAria = "My"; break;
                    case "status":
                        Status = SimpleParam(Parameter); break;
                    case "new":
                        Status = "1"; break;
                    default: break;
                }
            }
            
            // Запрашиваем поиск по БД
            List<string> Result = Get_Mol(CurUser, Structure, SearchAria, Status.ToInt(), UserID);

            // Отправляем ответ клиенту
            CurUser.Transport.SimpleMsg(handler, Result);
        }


        /// <summary>
        /// Поиск по подструктуре из БД с расшифровкой
        /// </summary>
        /// <param name="DataBase"></param>
        /// <param name="CurUser"></param>
        /// <param name="Sub_Mol"></param>
        /// <param name="Request"></param>
        /// <param name="Status"></param>
        /// <param name="UserID"></param>
        /// <returns></returns>
        private List<string> Get_Mol(User CurUser, string Sub_Mol = "", 
            string Request = "Permission", int Status = 0, string UserID=null)
        {
            //Создаём новые объекты
            List<string> Result = new List<string>(); //Список вывода

            //Создаём запрос на поиск
            string queryString = @"SELECT `id`, `name`, `laboratory`, `person`, `b_structure`, `state`,
`melting_point`, `conditions`, `other_properties`, `mass`, `solution`, `status` ";
            queryString += "\nFROM `molecules` \n";
            queryString += "WHERE (" + CurUser.GetPermissionsOrReqest(Request) + ")";
            if (Status > 0) queryString += " AND (`status` = " + Status.ToString() + ")"; // Добавляем статус в запрос
            if (UserID != "") queryString += " AND (`person` = " + UserID + ")"; // Ищем для конкретного пользователя

            DataTable dt = DataBase.Query(queryString);

            if (Sub_Mol == "")
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                    Result.Add(DataRow_To_Molecule_Transport(dt, i).ToXML());

            }

            else
            {
                // Сравнение каждой молекулы из запроса со стандартом
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    //Расшифровка
                    string Structure = Program.CommonAES.DecryptStringFromBytes(dt.Rows[i].ItemArray[4] as byte[]);

                    if (CheckMol(Sub_Mol, Structure))
                        Result.Add(DataRow_To_Molecule_Transport(dt, i).ToXML());
                };
            }

            DataBase.ConClose();
            return Result;
        }

        /// <summary>
        /// Проверка соответствия молекулы паттерну.
        /// </summary>
        /// <param name="Mol"></param>
        /// <param name="DB_Mol"></param>
        /// <returns></returns>
        private bool CheckMol(string Mol, string DB_Mol)
        {
            // Создаём объекты OpenBabel
            OBSmartsPattern SP = new OBSmartsPattern();
            OBConversion obconv = new OBConversion();
            obconv.SetInFormat("smi");
            OBMol mol = new OBMol();
            obconv.ReadString(mol, Mol);
            obconv.SetOutFormat("smi");

            string Temp = obconv.WriteString(mol);
            if (!mol.DeleteHydrogens()) { Console.WriteLine("DeleteHidrogens() failed!"); };  //Убираем все водороды
            
            string SubMol = System.Text.RegularExpressions.Regex.Replace(obconv.WriteString(mol), "[Hh ]", ""); //Убираем все водороды
            SP.Init(SubMol);  //Задаём структуру поиска в SMARTS

            obconv.SetInFormat("smi");
            obconv.ReadString(mol, DB_Mol); //Добавляем структуру из БД
            SP.Match(mol); //Сравниваем
            VectorVecInt Vec = SP.GetUMapList();
            if (Vec.Count > 0) { return true; } else { return false; }; //Возвращаем результат
        }


        /// <summary>
        /// Преобразование выдачи БД в формат для передачи клиенту
        /// </summary>
        /// <param name="DataBase"></param>
        /// <param name="dt"></param>
        /// <param name="i"></param>
        /// <returns></returns>
        private Molecule_Transport DataRow_To_Molecule_Transport(DataTable dt, int i)
        {
            /*
                                Структура данных: (-> - Открытый, => - закодированный)
                                -> New molecule
                                00 -> id
                                01 -> name
                                02 -> laboratory
                                03 -> person
                                04 => b_structure
                                05 => state
                                06 => melting_point
                                07 => conditions
                                08 => other_properties
                                09 => mass
                                10 => solution
                                11 -> laboratory_name
                                12 -> laboratory_Abb
                                13 -> name (person)
                                14 -> father's name
                                15 -> surname
                                16 -> job
                                17 -> status
                                18+ -> Виды анализа
                                */

            // Наполнение транспортного класса
            Molecule_Transport MT = new Molecule_Transport();
            MT.ID = Convert.ToInt32(FromBase(dt, i, 0));
            MT.Name = FromBase(dt, i, 1);
            MT.Laboratory = new laboratory();
            MT.Laboratory.ID = Convert.ToInt32(FromBase(dt, i, 2));
            List<string> Lab = GetRows("SELECT `name`, `abbr` FROM `laboratory` WHERE `id`=" +
                dt.Rows[i].ItemArray[2].ToString() + " LIMIT 1");
            MT.Laboratory.Name = Lab[0];
            MT.Laboratory.Abb = Lab[1];
            List<string> Per = GetRows(@"SELECT `name`, `fathers_name`, `Surname`, `job` 
                        FROM `persons` 
                        WHERE `id`= " + dt.Rows[i].ItemArray[3].ToString() + @"
                        LIMIT 1");
            MT.Person = new person();
            MT.Person.ID = Convert.ToInt32(FromBase(dt, i, 3));
            MT.Person.Name = Per[0];
            MT.Person.FathersName = Per[1];
            MT.Person.Surname = Per[2];
            MT.Person.Job = Per[3];
            MT.Structure = FromBaseDecrypt(dt, i, 4);
            MT.State = FromBaseDecrypt(dt, i, 5);
            MT.Melting_Point = FromBaseDecrypt(dt, i, 6);
            MT.Conditions = FromBaseDecrypt(dt, i, 7);
            MT.Other_Properties = FromBaseDecrypt(dt, i, 8);
            MT.Mass = FromBaseDecrypt(dt, i, 9);
            MT.Solution = FromBaseDecrypt(dt, i, 10);
            MT.Status = Convert.ToInt32(FromBase(dt, i, 11));
            MT.Analysis = GetRows(@"SELECT `analys`.`name`, `analys`.`name_whom` 
                        FROM `analys` 
                          INNER JOIN `analys_to_molecules` ON `analys_to_molecules`.`analys` = `analys`.`id`
                        WHERE `analys_to_molecules`.`molecule` = " + dt.Rows[i].ItemArray[0].ToString() + ";");

            // Добавляем список файлов
            MT.Files = new List<file>();
            //   Получаем файлы, имеющие отношение к данному соединению
            DataTable files = DataBase.Query(@"SELECT `file` 
                        FROM `files_to_molecules` 
                        WHERE `molecule` = " + dt.Rows[i].ItemArray[0].ToString() + ";");
            for (int f = 0; f < files.Rows.Count; f++)
            {
                file NF = new file();
                NF.ID = (int)files.Rows[f].ItemArray[0];
                DataTable NewFile = DataBase.Query(@"SELECT `name` FROM files WHERE `id`=" +
                    NF.ID.ToString() + @" LIMIT 1;");
                if (NewFile.Rows.Count == 0) { NF.Name = "Файл отсутствует"; }
                else { NF.Name = NewFile.Rows[0].ItemArray[0].ToString(); }

                MT.Files.Add(NF);
            }

            return MT;
        }

        
        /// <summary>
        /// NotNull без пробелов элемент из БД
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        private string FromBase(DataTable dt, int i, int j)
        {
            return NotNull(dt.Rows[i].ItemArray[j].ToString().Trim("\n"[0]));
        }

        /// <summary>
        /// NotNull без пробелов зашифрованный элемент из БД
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
        private string FromBaseDecrypt(DataTable dt, int i, int j)
        {
            return NotNull(ConsoleServer.Program.CommonAES.DecryptStringFromBytes(
                dt.Rows[i].ItemArray[j] as byte[])).Trim(new char[] { "\n"[0], ' ' });
        }
    }


}
