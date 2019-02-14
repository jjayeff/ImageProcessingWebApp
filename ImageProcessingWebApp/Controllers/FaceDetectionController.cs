using FaceAPI_MVC.Web.Helper;
using FaceAPI_MVC.Web.Models;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Vision.V1;
using Grpc.Auth;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace ImageProcessingWebApp.Controllers
{
    public class FaceDetectionController : Controller
    {
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        // | Config                                                          |
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        private static string ServiceKey = ConfigurationManager.AppSettings["FaceServiceKey"];
        private static string APIKey = ConfigurationManager.AppSettings["FaceAPIKey"];
        private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient(ServiceKey, APIKey);
        private static string directory = "../UploadedFiles";
        private static string UplImageName = string.Empty;
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        // | GET: FaceDetection/Index                                        |
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        public ActionResult Index()
        {
            return View();
        }
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        // | Google Cloud Vision Fucntion                                    |
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        public List<string> DetectDocumentText(string fileName)
        {
            var client = ImageAnnotatorClient.Create();
            // Load the image file into memory
            var image = Google.Cloud.Vision.V1.Image.FromFile(fileName);
            // Performs label detection on the image file
            List<string> lines = new List<string>();
            var annotations = client.DetectDocumentText(image);
            if (annotations != null)
            {
                var paragraphs = annotations.Pages
                .SelectMany(page => page.Blocks)
                .SelectMany(block => block.Paragraphs);
                foreach (var para in paragraphs)
                {
                    var box = para.BoundingBox;
                    //Console.WriteLine($"Bounding box: {string.Join(" / ", box.Vertices.Select(v => $"({v.X}, {v.Y})"))}");
                    var symbols = string.Join("", para.Words.SelectMany(w => w.Symbols).SelectMany(s => s.Text));
                    //Console.WriteLine($"Paragraph: {symbols}");
                    lines.Add(symbols);
                    //Console.WriteLine();
                }
            }
            //System.IO.File.WriteAllLines(result_path + "DetectDocumentText.txt", lines);
            return lines;
        }
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        // | Save File                                                       |
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        [HttpPost]
        public JsonResult SaveCandidateFiles()
        {
            string message = string.Empty, fileName = string.Empty, actualFileName = string.Empty; bool flag = false;
            //Requested File Collection
            HttpFileCollection fileRequested = System.Web.HttpContext.Current.Request.Files;
            if (fileRequested != null)
            {
                //Create New Folder
                CreateDirectory();

                //Clear Existing File in Folder
                ClearDirectory();

                for (int i = 0; i < fileRequested.Count; i++)
                {
                    var file = Request.Files[i];
                    actualFileName = file.FileName;
                    fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    int size = file.ContentLength;

                    try
                    {
                        file.SaveAs(Path.Combine(Server.MapPath(directory), fileName));
                        message = "File uploaded successfully";
                        UplImageName = fileName;
                        flag = true;
                    }
                    catch (Exception)
                    {
                        message = "File upload failed! Please try again";
                    }
                }
            }
            return new JsonResult
            {
                Data = new
                {
                    Message = message,
                    UplImageName = fileName,
                    Status = flag
                }
            };
        }
        public void CreateDirectory()
        {
            bool exists = System.IO.Directory.Exists(Server.MapPath(directory));
            if (!exists)
            {
                try
                {
                    Directory.CreateDirectory(Server.MapPath(directory));
                }
                catch (Exception ex)
                {
                    ex.ToString();
                }
            }
        }
        public void ClearDirectory()
        {
            DirectoryInfo dir = new DirectoryInfo(Path.Combine(Server.MapPath(directory)));
            var files = dir.GetFiles();
            if (files.Length > 0)
            {
                try
                {
                    foreach (FileInfo fi in dir.GetFiles())
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        fi.Delete();
                    }
                }
                catch (Exception ex)
                {
                    ex.ToString();
                }
            }
        }
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        // | Microsoft Azure Fucntion                                        |
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        [HttpGet]
        public async Task<dynamic> GetDetectedFaces()
        {
            ResultCollection.Clear();
            DetectedFaces.Clear();

            var DetectedResultsInText = string.Format("Detecting...");
            var FullImgPath = Server.MapPath(directory) + '/' + UplImageName as string;
            var QueryFaceImageUrl = directory + '/' + UplImageName;
            var OCRText = OCRImage(DetectDocumentText(FullImgPath));
            IdentifyResult[] results = { };
            if (UplImageName != "")
            {
                //Create New Folder
                CreateDirectory();

                try
                {
                    // Call detection REST API
                    using (var fStream = System.IO.File.OpenRead(FullImgPath))
                    {
                        // User picked one image
                        var imageInfo = UIHelper.GetImageInfoForRendering(FullImgPath);

                        // Create Instance of Service Client by passing Servicekey as parameter in constructor 
                        Face[] faces = await faceServiceClient.DetectAsync(fStream, true, true, new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Glasses });
                        var faceIds = faces.Select(face => face.FaceId).ToArray();
                        DetectedResultsInText = string.Format("{0} face(s) has been detected!!", faces.Length);
                        Bitmap CroppedFace = null;

                        string personGroupId = "myfriends";
                        await faceServiceClient.TrainPersonGroupAsync(personGroupId);
                        TrainingStatus trainingStatus = null;
                        while (true)
                        {
                            trainingStatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);
                            if (trainingStatus.Status != Status.Running)
                            {
                                break;
                            }

                            await Task.Delay(1000);
                        }
                        results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds);
                        int i = 0;
                        foreach (var face in faces)
                        {
                            //Create & Save Cropped Images
                            var croppedImg = Convert.ToString(Guid.NewGuid()) + ".jpeg" as string;
                            var croppedImgPath = directory + '/' + croppedImg as string;
                            var croppedImgFullPath = Server.MapPath(directory) + '/' + croppedImg as string;
                            CroppedFace = CropBitmap(
                                            (Bitmap)System.Drawing.Image.FromFile(FullImgPath),
                                            face.FaceRectangle.Left,
                                            face.FaceRectangle.Top,
                                            face.FaceRectangle.Width,
                                            face.FaceRectangle.Height);
                            CroppedFace.Save(croppedImgFullPath, ImageFormat.Jpeg);
                            if (CroppedFace != null)
                                ((IDisposable)CroppedFace).Dispose();

                            string confidence_name;
                            double confidence_value;
                            if (results[i++].Candidates.Length == 0)
                            {
                                confidence_name = "No one identified";
                                confidence_value = 0;
                            }
                            else
                            {
                                var candidateId = results[i - 1].Candidates[0].PersonId;
                                var confidence = results[i - 1].Candidates[0].Confidence;
                                var person = await faceServiceClient.GetPersonAsync(personGroupId, candidateId);
                                confidence_name = person.Name;
                                confidence_value = confidence;
                            }

                            DetectedFaces.Add(new vmFace()
                            {
                                ImagePath = FullImgPath,
                                FileName = croppedImg,
                                FilePath = croppedImgPath,
                                Left = face.FaceRectangle.Left,
                                Top = face.FaceRectangle.Top,
                                Width = face.FaceRectangle.Width,
                                Height = face.FaceRectangle.Height,
                                FaceId = face.FaceId.ToString(),
                                Gender = face.FaceAttributes.Gender,
                                Age = string.Format("{0:#} years old", face.FaceAttributes.Age),
                                IsSmiling = face.FaceAttributes.Smile > 0.0 ? "Smile" : "Not Smile",
                                Glasses = face.FaceAttributes.Glasses.ToString(),
                                Identify = confidence_name,
                                Confidence = (confidence_value * 100).ToString("0.######") + "%",
                            });
                        }

                        // Convert detection result into UI binding object for rendering
                        var rectFaces = UIHelper.CalculateFaceRectangleForRendering(faces, MaxImageSize, imageInfo);
                        foreach (var face in rectFaces)
                        {
                            ResultCollection.Add(face);
                        }
                    }
                }
                catch (FaceAPIException)
                {
                    //do exception work
                }
            }
            return new JsonResult
            {
                Data = new
                {
                    QueryFaceImage = QueryFaceImageUrl,
                    MaxImageSize = MaxImageSize,
                    FaceInfo = DetectedFaces,
                    FaceRectangles = ResultCollection,
                    DetectedResults = DetectedResultsInText,
                    OCRData = OCRText,
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        // | Other Fucntion                                                  |
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        public Bitmap CropBitmap(Bitmap bitmap, int cropX, int cropY, int cropWidth, int cropHeight)
        {
            Rectangle rect = new Rectangle(cropX, cropY, cropWidth, cropHeight);
            Bitmap cropped = bitmap.Clone(rect, bitmap.PixelFormat);
            return cropped;
        }
        public static string getBetween(string strSource, string strStart, string strEnd)
        {
            int Start, End;
            if (strSource.Contains(strStart))
            {
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;
                if (strEnd == "" || !strSource.Contains(strEnd))
                    End = strSource.Length;
                else
                    End = strSource.IndexOf(strEnd, Start);
                return strSource.Substring(Start, End - Start);
            }
            else
            {
                return "";
            }
        }
        private JsonResult OCRImage(List<string> detext_document_text)
        {
            string nameThai = "", nameEng = "", citizenId = "", dobThai = "", dobEng = "", religion = "", address = "";
            if (detext_document_text.Count > 0)
            {
                // name thai
                foreach (var value in detext_document_text)
                    if (getBetween(value, "ชื่อสกุล", "") != "")
                    {
                        nameThai += getBetween(value, "ชื่อสกุล", "") + "\r\n";
                        break;
                    }

                // name eng
                foreach (var value in detext_document_text)
                {
                    if (getBetween(value, "Name", "Lastname") != "")
                    {
                        nameEng += getBetween(value, "Name", "Lastname");
                    }
                    if (getBetween(value, "Lastname", "เกิด") != "")
                    {
                        nameEng += " " + getBetween(value, "Lastname", "เกิด");
                    }
                }

                // citizen id
                foreach (var value in detext_document_text)
                    if (getBetween(value, "ประจำตัวประชาชน", "") != "")
                    {
                        string b = string.Empty;
                        for (int i = 0; i < detext_document_text[1].Length; i++)
                        {
                            if (Char.IsDigit(detext_document_text[1][i]))
                                b += detext_document_text[1][i];
                        }
                        if (b != "")
                        {
                            citizenId += b + "\r\n";
                            break;
                        }
                    }

                //Date of Birth
                foreach (var value in detext_document_text)
                {
                    if (getBetween(value, "เกิดวันที่", "Date") != "")
                    {
                        dobThai += getBetween(value, "เกิดวันที่", "Date") + "\r\n";
                    }
                    if (getBetween(value, "Birth", "ศาสนา") != "")
                    {
                        dobEng += getBetween(value, "Birth", "ศาสนา") + "\r\n";
                    }
                }

                //Religion
                foreach (var value in detext_document_text)
                {
                    if (getBetween(value, "ศาสนา", "ที่อยู่") != "")
                    {
                        religion += getBetween(value, "ศาสนา", "ที่อยู่") + "\r\n";
                    }
                }

                //Address
                foreach (var value in detext_document_text)
                    if (getBetween(value, "ที่อยู่", "") != "")
                    {
                        address += getBetween(value, "ที่อยู่", "") + "\r\n";
                        break;
                    }
            }

            return new JsonResult
            {
                Data = new
                {
                    NameThai = nameThai,
                    NameEng = nameEng,
                    CitizenId = citizenId,
                    DOBThai = dobThai,
                    DOBEng = dobEng,
                    Religion = religion,
                    Address = address
                }
            };
        }
        public int MaxImageSize
        {
            get
            {
                return 450;
            }
        }
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        // | Set Model                                                       |
        // = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
        private ObservableCollection<vmFace> _detectedFaces = new ObservableCollection<vmFace>();
        private ObservableCollection<vmFace> _resultCollection = new ObservableCollection<vmFace>();
        public ObservableCollection<vmFace> DetectedFaces
        {
            get
            {
                return _detectedFaces;
            }
        }
        public ObservableCollection<vmFace> ResultCollection
        {
            get
            {
                return _resultCollection;
            }
        }
    }
}