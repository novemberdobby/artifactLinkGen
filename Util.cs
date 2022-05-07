namespace HadesBoonBot
{
    internal class Util
    {
        internal static string CreateDir(string path, bool deleteFirst = false)
        {
            bool exists = Directory.Exists(path);
            bool create = !exists || deleteFirst;

            if (deleteFirst && exists)
            {
                Directory.Delete(path, true);
            }

            if (create)
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }
    }
}
