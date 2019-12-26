using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Db;

namespace ChicagoSTDriveMgr
{
    public enum ReturnValue
    {
        Ok,
        ConnectionStringUndefined,
        ConnectionStringInvalidServer,
        ConnectionStringInvalidUserOrPassword,
        ConnectionStringInvalidDatabase,
        SqlError,
        DataEquNull,
        ColumnDoesntExist,
        RefGoodsError,
        RefFilesError,
        RefGoodsRefFilesError,
        CopyContentIntoFileOnServerByUriError,
        RefFilePacketsInsertError,
        DestinationDirectoryError,
        IdFieldDoesntExist,
        UrlFieldDoesntExist,
        ContentIsNotImage,
        WriteContentError,
        InvalidTableName,
        PhotoHostingNotUsed,
        NotRun,
        UnknownError
    }

    class Program
    {
        static int Main(string[] args)
        {
            var appConfig = new AppConfig();

            appConfig.Load();

            if (args.Length > 0)
                ParseCmdLine(args, appConfig);
            else
            {
                PrintHelp();
                return 0;
            }
            return (int)Core.Execute(appConfig);
        }

        static void ParseCmdLine(string[] args, AppConfig appConfig)
        {
            Regex
                regexSwitch = new Regex("^[\\-/]([^:]+)(?=$)"),
                regexSwitchWithValue = new Regex("^[\\-/](.+?):(.+)");

            for (var i = args.Length - 1; i >= 0; --i)
            {
                var match = regexSwitch.Match(args[i]);

                if (match.Success && match.Groups.Count == 2)
                {
                    switch (match.Groups[1].Value.ToLower(CultureInfo.InvariantCulture))
                    {
                        case "movereffilestofilehosting":
                            {
                                appConfig.MoveRefFilesToFileHosting = true;
                                break;
                            }
                        case "uploaddocs":
                            {
                                appConfig.Upload = true;
                                break;
                            }
                        case "uploadmarketingmterials":
                            {
                                appConfig.UploadMM = true;
                                break;
                            }
                        case "uploadpromo":
                        {
                            appConfig.UploadPromo = true;
                            break;
                        }
                        case "cleardoubles":
                        {
                            appConfig.ClearDoubles = true;
                            break;
                        }
                        case "help":
                            {
                                PrintHelp();
                                Environment.Exit(0);
                                return;
                            }
                    }

                    continue;
                }

                match = regexSwitchWithValue.Match(args[i]);

                if (!match.Success || match.Groups.Count != 3)
                    continue;

                string
                    @switch = match.Groups[1].Value.ToLower(),
                    value = match.Groups[2].Value;

                switch (@switch)
                {
                    case "d":
                        {
                            appConfig.DestinationPath = value;
                            break;
                        }
                    case "datebegin":
                        {
                            appConfig.DateBegin = DateTime.Parse(value);
                            break;
                        }
                    case "dateend":
                        {
                            appConfig.DateEnd = DateTime.Parse(value);
                            break;
                        }
                    case "count":
                        {
                            appConfig.CountRows = int.TryParse(value, out int len) ? len : 100;
                            break;
                        }

                }
            }
        }

        static void PrintHelp()
        {
            var lines = new[]
            {
               "*******************************************************************************",
               "******************Утилита передачи фотографий в ST Drive***********************",
               "*******************************************************************************",
               "Описание параметров вызова командной строки",
               "*******************************************************************************",
               "-movereffilestofilehosting - копирование фотографий из товаров в STDrive",
               "*******************************************************************************",
               "-d://server/path - загрузка фотографий документов из STDrive в указанную папку",
               "*******************************************************************************",
               "-uploaddocs - выгрузка фотографий документов в STDrive за указанный период",
               "-datebegin:DD-MM-YYYY - дата начала периода",
               "-dateend:DD-MM-YYYY - дата конца периода",
               "*******************************************************************************",
               "-uploadmarketingmterials - выгрузка ММ",
               "-uploadpromo - выгрузка акций",
               "-count:n - количество записей в одном запросе",
               "*******************************************************************************",
               "-cleardoubles развязка дублей в refFiles",
               "*******************************************************************************",
               "-help подсказка по параметрам вызова",
               "*******************************************************************************",
               "Нажмите Enter для выхода"
            };
            var old = Console.OutputEncoding;
            var utfEncoding = Encoding.UTF8;

            var enc = Encoding.GetEncoding(866);
            Console.OutputEncoding = enc;


            foreach (var line in lines)
            {
                var bytes = utfEncoding.GetBytes(line);
                var encoded = Encoding.Convert(utfEncoding, enc, bytes);
                Console.WriteLine(enc.GetString(encoded));
            }

            Console.ReadLine();
            Console.OutputEncoding = old;
        }
    }
}
