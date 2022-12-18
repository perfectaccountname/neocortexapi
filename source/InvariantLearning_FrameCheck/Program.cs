using NeoCortexApi;
using NeoCortexApi.Entities;
using System.Diagnostics;
using InvariantLearning_FrameCheck;
using Invariant.Entities;
using System.Collections.Concurrent;
using NeoCortexApi.Encoders;
using HtmImageEncoder;
using NeoCortexApi.Classifiers;
using NeoCortexApi.Network;

namespace InvariantLearning_FrameCheck
{
    public class InvariantLearning
    {
        public static void Main()
        {
            string experimentTime = DateTime.UtcNow.ToLongDateString().ToString().Replace(',', ' ') + " " + DateTime.UtcNow.ToLongTimeString().ToString().Replace(':', '_');
            // Invariant Learning Experiment
            InvariantRepresentation($"HtmInvariantLearning {experimentTime}");
        }

        /// <summary>
        /// Latest Experiment
        /// </summary>
        /// <param name="experimentFolder"></param>
        private static void InvariantRepresentation(string experimentFolder)
        {
            #region Samples taking
            List<Sample> trainingSamples = new List<Sample>();
            List<Sample> trainingBigSamples = new List<Sample>();
            List<Sample> testingSamples = new List<Sample>();
            List<Sample> sourceSamples = new List<Sample>();

            Utility.CreateFolderIfNotExist(experimentFolder);

            // Get the folder of MNIST archives tar.gz files.
            string sourceMNIST = Path.Combine(experimentFolder, "MnistSource");
            Utility.CreateFolderIfNotExist(sourceMNIST);
            Mnist.DataGen("MnistDataset", sourceMNIST, 15);

            // generate 32x32 source MNISTDataSet
            int imageWidth = 32; int imageHeight = 32;
            int frameWidth = 16; int frameHeight = 16;
            DataSet sourceSet = new DataSet(sourceMNIST);

            DataSet sourceSet_32x32 = DataSet.ScaleSet(experimentFolder, imageWidth, imageHeight, sourceSet, "sourceSet");
            DataSet testSet_32x32 = sourceSet_32x32.GetTestData(10);

            DataSet scaledTestSet = DataSet.CreateTestSet(testSet_32x32, 100, 100, Path.Combine(experimentFolder, "testSet_100x100"));
            DataSet scaledTrainSet = DataSet.CreateTestSet(sourceSet_32x32, 100, 100, Path.Combine(experimentFolder, "trainSet_100x100"));

            // write extracted/filtered frame from 32x32 dataset into 4x4 for SP to learn all pattern
            //var listOfFrame = Frame.GetConvFrames(imageWidth, imageHeight, frameWidth, frameHeight, 4, 4);
            var listOfFrame = Frame.GetConvFramesbyPixel(32, 32, frameWidth, frameHeight, 4);
            string extractedFrameFolder = Path.Combine(experimentFolder, "extractedFrameTraining");
            string extractedBigFrameFolder = Path.Combine(experimentFolder, "extractedBigFrameTraining");
            string extractedFrameFolderBinarized = Path.Combine(experimentFolder, "extractedFrameBinarized");
            int index = 0;
            List<string> frameDensityList = new List<string>();

            string extractedFrameFolderTest = Path.Combine(experimentFolder, "extractedFrameTesting");

            //
            // Creating the training frames for each images and put them in folders.
            foreach (var image in sourceSet_32x32.Images)
            {
                Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolder, $"{image.Label}"));
                //Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolderBinarized, $"{image.Label}"));
                foreach (var frame in listOfFrame)
                {
                    if (image.IsRegionInDensityRange(frame, 25, 80))
                    {
                        if (!DataSet.ExistImageInDataSet(image, extractedFrameFolder, frame))
                        {
                            string savePath = Path.Combine(extractedFrameFolder, $"{image.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");

                            image.SaveTo(savePath, frame, true);

                            frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
                            index += 1;
                        }
                    }
                }
                index = 0;
            }

            //
            // Create training samples from the extracted frames.
            foreach (var classFolder in Directory.GetDirectories(extractedFrameFolder))
            {
                string label = Path.GetFileName(classFolder);
                foreach (var imagePath in Directory.GetFiles(classFolder))
                {
                    var fileName = Path.GetFileNameWithoutExtension(imagePath);
                    var coordinatesString = fileName.Split('_').ToList();
                    List<int> coorOffsetList = new List<int>();
                    foreach (var coordinates in coordinatesString)
                    {
                        coorOffsetList.Add(int.Parse(coordinates));
                    }

                    //
                    // Calculate offset coordinates.
                    var tlX = coorOffsetList[0] = 0 - coorOffsetList[0];
                    var tlY = coorOffsetList[1] = 0 - coorOffsetList[1];
                    var brX = coorOffsetList[2] = imageWidth - coorOffsetList[2] - 1;
                    var brY = coorOffsetList[3] = imageHeight - coorOffsetList[3] - 1;

                    Sample sample = new Sample();
                    sample.Object = label;
                    sample.FramePath = imagePath;
                    sample.Position = new Frame(tlX, tlY, brX, brY);
                    trainingSamples.Add(sample);
                }
            }

            //
            // Creating the testing frames for each images and put them in folders.
            Utility.CreateFolderIfNotExist(extractedFrameFolderTest);
            //listOfFrame = Frame.GetConvFrames(80, 80, frameWidth, frameHeight, 10, 10);
            listOfFrame = Frame.GetConvFramesbyPixel(100, 100, frameWidth, frameHeight, 4);
            index = 0;
            foreach (var testImage in scaledTestSet.Images)
            {
                Utility.CreateFolderIfNotExist(Path.Combine(extractedFrameFolderTest, $"{testImage.ImagePath.Substring(testImage.ImagePath.Length - 5, 1)}", $"{testImage.Label}"));
                foreach (var frame in listOfFrame)
                {
                    if (testImage.IsRegionInDensityRange(frame, 25, 80))
                    {
                        if (!DataSet.ExistImageInDataSet(testImage, extractedFrameFolderTest, frame))
                        {
                            string savePath = Path.Combine(extractedFrameFolderTest, $"{testImage.ImagePath.Substring(testImage.ImagePath.Length - 5, 1)}", $"{testImage.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");

                            testImage.SaveTo(savePath, frame, true);

                            frameDensityList.Add($"pattern {index}, Pixel Density {testImage.FrameDensity(frame, 255 / 2) * 100}");
                            index += 1;
                        }
                    }
                }
                index = 0;
            }

            //
            // Creating the big training frames for each images and put them in folders.
            //listOfFrame = Frame.GetConvFrames(80, 80, frameWidth, frameHeight, 10, 10);
            var listOfBigFrame = Frame.GetConvFramesbyPixel(100, 100, imageWidth, imageHeight, 1);
            index = 0;
            foreach (var image in scaledTrainSet.Images)
            {
                Utility.CreateFolderIfNotExist(Path.Combine(extractedBigFrameFolder, $"{image.Label}"));
                double minDensity = 45;
                string savePath = "";
            restart:
                foreach (var frame in listOfBigFrame)
                {
                    if (image.IsRegionInDensityRange(frame, minDensity, 80))
                    {
                        if (!DataSet.ExistImageInDataSet(image, extractedBigFrameFolder, frame))
                        {

                            savePath = Path.Combine(extractedBigFrameFolder, $"{image.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}.png");

                            image.SaveTo(savePath, frame, true);

                            frameDensityList.Add($"pattern {index}, Pixel Density {image.FrameDensity(frame, 255 / 2) * 100}");
                            index += 1;
                        }
                    }
                    if ((frame == listOfBigFrame.Last()) && string.IsNullOrEmpty(savePath))
                    {
                        minDensity -= 1;
                        if (minDensity < 10)
                        {
                            break;
                        }
                        goto restart;
                    }
                }
                index = 0;
            }
            //listOfBigFrame = Frame.GetConvFramesbyPixel(32, 32, 32, 32, 4);
            //foreach (var image in sourceSet_32x32.Images)
            //{
            //    foreach (var frame in listOfBigFrame)
            //    {
            //        var savePath = Path.Combine(extractedBigFrameFolder, $"{image.Label}", $"{frame.tlX}_{frame.tlY}_{frame.brX}_{frame.brY}_{sourceSet_32x32.Images.IndexOf(image)}.png");
            //        image.SaveTo(savePath, frame, true);
            //    }
            //}

            //
            // Create big training samples from the extracted frames.
            foreach (var classFolder in Directory.GetDirectories(extractedBigFrameFolder))
            {
                string label = Path.GetFileName(classFolder);
                foreach (var imagePath in Directory.GetFiles(classFolder))
                {
                    var fileName = Path.GetFileNameWithoutExtension(imagePath);
                    var coordinatesString = fileName.Split('_').ToList();
                    List<int> coorOffsetList = new List<int>();
                    foreach (var coordinates in coordinatesString)
                    {
                        coorOffsetList.Add(int.Parse(coordinates));
                    }

                    //
                    // Calculate offset coordinates.
                    var tlX = coorOffsetList[0];
                    var tlY = coorOffsetList[1];
                    var brX = coorOffsetList[2];
                    var brY = coorOffsetList[3];

                    Sample sample = new Sample();
                    sample.Object = label;
                    sample.FramePath = imagePath;
                    sample.Position = new Frame(tlX, tlY, brX, brY);
                    trainingBigSamples.Add(sample);
                }
            }


            DataSet trainingSet = new DataSet(extractedFrameFolder);
            DataSet trainingBigSet = new DataSet(extractedBigFrameFolder);

            //a
            // Create the big testing frame
            //string[] digits = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
            //foreach (var digit in digits)
            //{
            //    //
            //    // training images.
            //    string digitTrainingFolder = Path.Combine(experimentFolder, "sourceSet_32x32", digit);
            //    var trainingImages = Directory.GetFiles(digitTrainingFolder);

            //    foreach (string image in trainingImages)
            //    {
            //        Sample sample = new Sample();
            //        var imageName = Path.GetFileName(image);
            //        sample.FramePath = Path.Combine(digitTrainingFolder, imageName);
            //        sample.Object = digit;

            //        sourceSamples.Add(sample);
            //    }
            //}

            #endregion

            #region Config
            Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(InvariantRepresentation)}");

            int inputBits = 256;
            int numColumns = 1024;

            #endregion

            #region Run experiment
            HtmClassifier<string, ComputeCycle> cls = new HtmClassifier<string, ComputeCycle>();

            var numUniqueInputs = trainingSamples.Count;

            LearningUnit learningUnit1 = new LearningUnit(16, 16, numColumns, "placeholder");
            LearningUnit learningUnit2 = new LearningUnit(32, 32, numColumns*4, "placeholder");
            learningUnit1.TrainingNewbornCycle(trainingSet);
            learningUnit2.TrainingNewbornCycle(trainingBigSet);

            //
            // Add the stable SDRs to samples.
            List<Sample> samples = new List<Sample>();
            foreach (var trainingSample in trainingSamples)
            {
                //var lyrOut1 = layer1.Compute(trainingSample.FramePath, false);
                //var activeColumns = layer1.GetResult("sp1") as int[];

                var activeColumns = learningUnit1.Predict(trainingSample.FramePath);
                if (activeColumns != null && activeColumns.Length != 0)
                {
                    trainingSample.PixelIndicies = new int[activeColumns.Length];
                    trainingSample.PixelIndicies = activeColumns;
                    samples.Add(trainingSample);
                }
            }
            cls.LearnObj(samples);

            List<Sample> bigSamples = new List<Sample>();
            foreach (var bigSample in trainingBigSamples)
            {
                var activeColumns = learningUnit2.Predict(bigSample.FramePath);
                var sdrBinArray = learningUnit2.ToSDRBinArray(activeColumns);
                if (activeColumns != null && activeColumns.Length != 0)
                {
                    bigSample.PixelIndicies = new int[activeColumns.Length];
                    bigSample.PixelIndicies = activeColumns;
                    bigSamples.Add(bigSample);
                }
            }
            cls.LearnMnistObj(bigSamples);

            //
            // Create testing samples from the extracted frames.
            string[] directories = System.IO.Directory.GetDirectories(extractedFrameFolderTest, "*", System.IO.SearchOption.TopDirectoryOnly);
            var directoryCount = 0;
            foreach (string directory in directories)
            {
                var directoryPath = Path.Combine(experimentFolder, $"predictedFrames_{directoryCount}");
                directoryCount++;
                Utility.CreateFolderIfNotExist(directoryPath);
                foreach (var classFolder in Directory.GetDirectories(directory))
                {
                    string label = Path.GetFileName(classFolder);
                    foreach (var imagePath in Directory.GetFiles(classFolder))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(imagePath);
                        var coordinates = fileName.Split('_');
                        var tlX = int.Parse(coordinates[0]);
                        var tlY = int.Parse(coordinates[1]);
                        var blX = int.Parse(coordinates[2]);
                        var brY = int.Parse(coordinates[3]);
                        Sample sample = new Sample();
                        sample.Object = label;
                        sample.FramePath = imagePath;
                        sample.Position = new Frame(tlX, tlY, blX, brY);
                        testingSamples.Add(sample);
                    }
                }

                //
                // Create and add SDRs for the testing samples.
                foreach (var testingSample in testingSamples)
                {
                    //var lyrOut1 = layer1.Compute(testingSample.FramePath, false);
                    //var activeColumns = layer1.GetResult("sp1") as int[];

                    var activeColumns = learningUnit1.Predict(testingSample.FramePath);
                    if (activeColumns != null)
                    {
                        testingSample.PixelIndicies = new int[activeColumns.Length];
                        testingSample.PixelIndicies = activeColumns;
                    }
                }

                //
                // Classifying each testing sample.
                var testingSamplesDict = testingSamples.Select(x => x).GroupBy(x => x.Object).ToDictionary(group => group.Key, group => group.ToList());
                double match = 0;
                foreach (var item in testingSamplesDict)
                {
                    //var predictedObj = cls.PredictObj(item.Value, 5);
                    var predictedObj = cls.PredictObj2(item.Value, 10);
                    var itemFolderPath = Path.Combine(directoryPath, $"{item.Key}");
                    Utility.CreateFolderIfNotExist(itemFolderPath);
                    double bestPixelDensity = 0.0;
                    var testImages = scaledTestSet.Images.Where(x => x.Label == item.Key).ToList();
                    foreach (var testImage in testImages)
                    {
                        Dictionary<Frame, double> frameDensityDict = new Dictionary<Frame, double>();
                        double minDensity = 10.0;
                        foreach (var obj in predictedObj)
                        {
                            var frame = obj.Position;
                            double whitePixelDensity = testImage.FrameDensity(frame, minDensity);
                            frameDensityDict.Add(frame, whitePixelDensity);
                        }
                        Frame chosenFrame = frameDensityDict.OrderByDescending(g => g.Value).Select(g => g.Key).First();
                        var savePath = Path.Combine(itemFolderPath, $"{testImage.Label}.png");
                        testImage.SaveTo(savePath, chosenFrame, true);
                    }
                    
                    var savePathList = Directory.GetFiles(itemFolderPath).ToList();
                    List<string> results = new List<string>();
                    foreach (var path in savePathList)
                    {
                        var activeColumns = learningUnit2.Predict(path);

                        var sdrBinArray = learningUnit2.ToSDRBinArray(activeColumns);
                        var res = cls.ValidateObj(activeColumns, 2);
                        results.AddRange(res);
                    }
                    var resultOrder = results.GroupBy(x => x)
                                            .OrderByDescending(g => g.Count())
                                            .Select(g => g.Key).ToList();
                    var bestResult = resultOrder.First();
                    if (bestResult == item.Key)
                    {
                        match++;
                    }
                }

                //
                // Calculate Accuracy
                double numOfItems = testingSamplesDict.Count();
                var accuracy = (match / numOfItems)*100;
                testingSamples.Clear();
                testingSamplesDict.Clear();
            }
            Debug.WriteLine("------------ END ------------");
            #endregion
        }

        private static void SPCapacityTest()
        {
            Dictionary<string, object> settings = new Dictionary<string, object>()
            {
                { "W", 15},
                { "N", 100},
                { "Radius", -1.0},
                { "MinVal", 0.0},
                { "Periodic", false},
                { "Name", "scalar"},
                { "ClipInput", false},
                { "MaxVal", (double)600}
            };

            EncoderBase encoder = new ScalarEncoder(settings);

            bool isInStableState = false;
            HtmConfig htm = new HtmConfig(new int[] { 100 }, new int[] { 1024 });
            Connections conn = new(htm);
            HomeostaticPlasticityController hpc = new HomeostaticPlasticityController(conn, 600 * 100, (isStable, numPatterns, actColAvg, seenInputs) =>
            {
                if (isStable)
                    // Event should be fired when entering the stable state.
                    Debug.WriteLine($"STABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");
                else
                    // Ideal SP should never enter unstable state after stable state.
                    Debug.WriteLine($"INSTABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");

                // We are not learning in instable state.
                isInStableState = isStable;

                // Clear active and predictive cells.
                //tm.Reset(mem);
            }, numOfCyclesToWaitOnChange: 100);
            SpatialPooler sp = new SpatialPooler(hpc);
            sp.Init(conn);
            while (!isInStableState)
            {
                for (double i = 0; i < 600; i += 1)
                {
                    sp.Compute(encoder.Encode(i), true);
                }
            }
            hpc.TraceState();
        }
    }
}