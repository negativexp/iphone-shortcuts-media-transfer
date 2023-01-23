using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using static System.Net.WebRequestMethods;

namespace ShortcutsListener
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            int port = 2560;
            byte[] responseBytes = Encoding.ASCII.GetBytes(HTTPRequest.BasicResponse);
            TcpListener server = new TcpListener(IPAddress.Any, port);
            int filecounter = 1;
            string dirPath = "";
            string finalFile = "";

            Console.WriteLine("Please type in a folder name that exists or one will be created");
            string folderInput = Console.ReadLine();
            if(System.IO.Directory.Exists(folderInput))
            {
                Console.WriteLine($"Folder {folderInput} has been found!");
                dirPath = folderInput + "\\";
            }
            else
            {
                dirPath = folderInput + "\\";
                System.IO.Directory.CreateDirectory(folderInput);
                Console.WriteLine($"Folder {folderInput} has been created!");
            }

            Console.WriteLine("starting local server...");
            server.Start();

            while (true)
            {
                Console.WriteLine($"Listening on port {port}");
                TcpClient client = server.AcceptTcpClient();  //if a connection exists, the server will accept it
                NetworkStream ns = client.GetStream(); //networkstream is used to send/receive messages

                HTTPRequest.Parse(ns);

                //get the file size 
                if (HTTPRequest.Headers.ContainsKey(HTTPReqHeaderKey.ContentLength) && int.TryParse(HTTPRequest.Headers[HTTPReqHeaderKey.ContentLength], out int numberOfbytesToRead))
                {
                    string fileName = "";
                    if (HTTPRequest.Headers.ContainsKey(HTTPReqHeaderKey.FileName))
                    {
                        fileName = HTTPRequest.Headers[HTTPReqHeaderKey.FileName];
                    }
                    else //if file name is not specified generate something unique
                    {
                        fileName = $"file_{Guid.NewGuid()}";
                        
                    }

                    //get the extention of the file its been always image/*, video/*, */*
                    string fileExtention = HTTPRequest.Headers[HTTPReqHeaderKey.ContentType].Split('/')[1].ToLower();

                    switch (fileExtention)
                    {
                        case "plain":
                            fileExtention = "txt";
                            break;
                        case "quicktime": //ios puts video/quicktime content-type header for their video files.
                            fileExtention = "mov";
                            break;
                        default:
                            break;
                    }

                    finalFile = dirPath + fileName + "." + fileExtention;

                    byte[] buffer = new byte[10000];
                    int readCounter = 0;
                    int numberOfBytesRead = 0;

                    byte[] bytes = new byte[numberOfbytesToRead];
                    readCounter = 0;
                    while (readCounter < numberOfbytesToRead)
                    {
                        numberOfBytesRead = ns.Read(buffer, 0, buffer.Length);
                        Array.Copy(buffer, 0, bytes, readCounter, numberOfBytesRead);
                        readCounter += numberOfBytesRead;
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write($"{((UInt64)readCounter * 100) / ((UInt64)numberOfbytesToRead - 1)}% Completed" + " ; " + "Files total:" + filecounter);
                    }

                    using(var stream = new MemoryStream(bytes))
                    {
                        var metadata = ImageMetadataReader.ReadMetadata(stream);

                        var subdir = metadata.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                        

                        if (subdir?.GetDescription(ExifDirectoryBase.TagDateTime) != null)
                        {
                            DateTime dt = DateTime.ParseExact(subdir?.GetDescription(ExifDirectoryBase.TagDateTime), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                            string output = dt.ToString("dd-MM-yyyy HH-mm-ss", CultureInfo.InvariantCulture);
                            finalFile = dirPath + output + "." + fileExtention;
                            Console.WriteLine($"\n \n {fileName}");
                        }
                        else if (subdir?.GetDescription(ExifDirectoryBase.TagDateTimeDigitized) != null)
                        {
                            DateTime dt = DateTime.ParseExact(subdir?.GetDescription(ExifDirectoryBase.TagDateTimeDigitized), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                            string output = dt.ToString("dd-MM-yyyy HH-mm-ss", CultureInfo.InvariantCulture);
                            finalFile = dirPath + output + "." + fileExtention;
                            Console.WriteLine($"\n \n {fileName}");
                        }
                        else if (subdir?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal) != null)
                        {
                            DateTime dt = DateTime.ParseExact(subdir?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                            string output = dt.ToString("dd-MM-yyyy HH-mm-ss", CultureInfo.InvariantCulture);
                            finalFile = dirPath + output + "." + fileExtention;
                            Console.WriteLine($"\n \n {fileName}");
                        }
                        //else
                        //{
                        //    fileName = dirPath + filecounter + "." + fileExtention;
                        //    Console.WriteLine($"\n \n {fileName}");
                        //    Console.WriteLine(subdir?.GetDescription(ExifDirectoryBase.TagDocumentName));
                        //}

                        stream.Close();
                    }
                    using(FileStream fs = new FileStream(finalFile, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(bytes, 0, bytes.Length);
                        fs.Flush();
                        fs.Close();
                    }
                    Console.WriteLine('\n');
                }
                ns.Write(responseBytes, 0, responseBytes.Length);
                ns.Close();
                client.Close();
                filecounter++;
            }
        }
    }
}
