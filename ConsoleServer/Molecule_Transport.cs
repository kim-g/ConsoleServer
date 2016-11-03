using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace ConsoleServer
{
    public class Molecule_Transport
    {
        public int ID;
        public string Name;
        public laboratory Laboratory;
        public person Person;
        public string Structure;
        public string State;
        public string Melting_Point;
        public string Conditions;
        public string Other_Properties;
        public string Mass;
        public string Solution;
        public int Status;
        public List<string> Analysis;

        public string ToXML()
        {
            // передаем в конструктор тип класса
            XmlSerializer formatter = new XmlSerializer(typeof(Molecule_Transport));

            // получаем поток, куда будем записывать сериализованный объект
            using (MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, this);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public void Save(string FileName)
        {
            // передаем в конструктор тип класса
            XmlSerializer formatter = new XmlSerializer(typeof(Molecule_Transport));

            // получаем поток, куда будем записывать сериализованный объект
            using (FileStream fs = new FileStream(FileName, FileMode.OpenOrCreate))
            {
                formatter.Serialize(fs, this);
            }
        }
    }

    public class laboratory
    {
        public int ID;
        public string Name;
        public string Abb;
    }

    public class person
    {
        public int ID;
        public string Name;
        public string FathersName;
        public string Surname;
        public string Job;
    }
}
