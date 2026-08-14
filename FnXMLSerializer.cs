using System;
using System.IO;
using System.Xml.Serialization;

namespace SWM
{
    public static class FnXMLSerializer<T>
    {
        public static bool Save(string path, T target)
        {
            try
            {
                using (StreamWriter streamWriter = new StreamWriter(path))
                    new XmlSerializer(typeof(T)).Serialize((TextWriter)streamWriter, (object)target);
                return true;
            }
            catch (IOException ex)
            {
                Console.WriteLine("Error reading from {0}. Message = {1}", (object)path, (object)ex.Message);
                return false;
            }
        }

        public static bool Load(string path, out T target)
        {
            bool flag;
            try
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
                using (StreamReader streamReader = new StreamReader(path))
                    target = (T)xmlSerializer.Deserialize((TextReader)streamReader);
                flag = true;
            }
            catch (IOException ex)
            {
                Console.WriteLine("Error reading from {0}. Message = {1}", (object)path, (object)ex.Message);
                flag = false;
                target = default(T);
            }
            return flag;
        }
    }
}
