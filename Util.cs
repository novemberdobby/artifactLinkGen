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

        internal static void DownloadFile(HttpClient client, string url, string targetFile, bool dontOverwrite = true)
        {
            if (!(File.Exists(targetFile) && dontOverwrite))
            {
                using var stream = client.GetStreamAsync(url).Result;
                using var fstream = new FileStream(targetFile, FileMode.CreateNew);
                stream.CopyToAsync(fstream).Wait();
            }
        }
    }
}
