using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Concurrent;

class Program
{
    static void Main(string[] args)
    {
        string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
        ConfigurationManager configManager = new ConfigurationManager(configFilePath);

        string dataStoragePath = configManager.GetDataStoragePath();
        if (string.IsNullOrEmpty(dataStoragePath))
        {
            // Default data storage path if not set in config.ini
            dataStoragePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Console.WriteLine($"Data storage path not found in config.ini. Using default path: {dataStoragePath}");
            Directory.CreateDirectory(dataStoragePath); // Create the directory if it doesn't exist
            configManager.SetDataStoragePath(dataStoragePath);
        }

        DNCCleaner cleaner = new DNCCleaner(dataStoragePath);
        cleaner.LoadDNCLists();
        cleaner.CleanFiles();
    }
}

class DNCCleaner
{
  private ConcurrentDictionary dncNumbers;
  private string baseDirectory;
  
  public DNCCleaner(string baseDirectory)
  {
      this.baseDirectory = baseDirectory;
      dncNumbers = new ConcurrentDictionary<string, bool>();
  }
  
  public void LoadDNCLists()
  {
      string dncPath = Path.Combine(baseDirectory, "DNC-Lists");
      foreach (var file in Directory.GetFiles(dncPath))
      {
          foreach (var line in File.ReadLines(file))
          {
              dncNumbers[line.Trim()] = true;
          }
      }
  }
  
  public void CleanFiles()
  {
      string sourcePath = Path.Combine(baseDirectory, "TOBECLEANED");
      string destinationPath = Path.Combine(baseDirectory, "CLEANEDDATA");
  
      Directory.CreateDirectory(destinationPath);
  
      var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
      Parallel.ForEach(Directory.GetFiles(sourcePath), options, file =>
      {
          string destFile = Path.Combine(destinationPath, Path.GetFileName(file));
          using (var writer = new StreamWriter(destFile))
          {
              bool isFirstLine = true;
              foreach (var line in File.ReadLines(file))
              {
                  if (isFirstLine)
                  {
                      isFirstLine = false;
                      if (IsHeaderRow(line))
                      {
                          writer.WriteLine(line);
                          continue;
                      }
                  }
  
                  var columns = line.Split(',');
                  if (columns.Any(c => IsValidPhoneNumber(c) && dncNumbers.ContainsKey(FormatPhoneNumber(c))))
                  {
                      continue;
                  }
  
                  writer.WriteLine(line);
              }
          }
      });
  }
  
  private bool IsHeaderRow(string line)
  {
      // Implement logic to determine if the row is a header
      return line.Split(',').Any(cell => !Regex.IsMatch(cell, @"^(1|\+1)?\d{10}$"));
  }
  
  private bool IsValidPhoneNumber(string number)
  {
      return Regex.IsMatch(number, @"^(1|\+1)?\d{10}$");
  }
  
  private string FormatPhoneNumber(string number)
  {
      return Regex.Replace(number, @"[^\d]", "");
  }
}

class ConfigurationManager
{
    private string configFilePath;

    public ConfigurationManager(string configFilePath)
    {
        this.configFilePath = configFilePath;
    }

    public string GetDataStoragePath()
    {
        if (File.Exists(configFilePath))
        {
            string path = File.ReadAllText(configFilePath);
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        return null;
    }

    public void SetDataStoragePath(string path)
    {
        File.WriteAllText(configFilePath, path);
    }
}
