using NeoCortexApi;
using NeoCortexApi.Encoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static NeoCortexApiSample.MultiSequenceLearning;

namespace NeoCortexApiSample
{
    class Program
    {
        /// <summary>
        /// This sample shows a typical experiment code for SP and TM.
        /// You must start this code in debugger to follow the trace.
        /// and TM.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            //
            // Starts experiment that demonstrates how to learn spatial patterns.
            //SpatialPatternLearning experiment = new SpatialPatternLearning();
            //experiment.Run();

            //
            // Starts experiment that demonstrates how to learn spatial patterns.
            //SequenceLearning experiment = new SequenceLearning();
            //experiment.Run();

            // RunMultiSimpleSequenceLearningExperiment();
            // RunMultiSequenceLearningExperiment();
            RunObjectRecognitionExperiment();
        }

        private static void RunObjectRecognitionExperiment()
        {
            List<Sample> trainingSamples = new List<Sample>();
            List<Sample> testingSamples = new List<Sample>();
            //Sample sample = new Sample();
            //Sample sample1 = new Sample();
            //Sample sample2 = new Sample();
            string trainingFolder = new string("MnistPng28x28_smallerdataset\\training");
            string testingFolder = new string("MnistPng28x28_smallerdataset\\testing");
            string[] digits = new string[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

            // TODO
            //sample.Feature add odd/even

            //sample.Feature.Add("shape", Path.Combine("MnistPng28x28_smallerdataset", "training", "0", "108.png"));
            //sample.Feature.Add("parity", 20.0);
            //sample.Feature.Add("object", 0.0);

            //sample1.Feature.Add("shape", Path.Combine("MnistPng28x28_smallerdataset", "training", "0", "114.png"));
            //sample1.Feature.Add("parity", 20.0);
            //sample1.Feature.Add("object", 0.0);

            //sample2.Feature.Add("shape", Path.Combine("MnistPng28x28_smallerdataset", "training", "1", "1002.png"));
            //sample2.Feature.Add("parity", 19.0);
            //sample2.Feature.Add("object", 1.0);

            //samples.Add(sample);
            //samples.Add(sample1);
            //samples.Add(sample2);

            string testOutputFolder = $"Output-{nameof(RunObjectRecognitionExperiment)}";
            if (Directory.Exists(testOutputFolder))
                Directory.Delete(testOutputFolder, true);

            Directory.CreateDirectory(testOutputFolder);


            //List<string> trainingFiles = new List<string>();

            // Active columns of every specific file.
            Dictionary<string, Dictionary<string, int[]>> fileActCols = new Dictionary<string, Dictionary<string, int[]>>();

            foreach (var digit in digits)
            {
                //
                // training images.
                string digitTrainingFolder = Path.Combine(trainingFolder, digit);

                if (!Directory.Exists(digitTrainingFolder))
                    continue;

                var trainingImages = Directory.GetFiles(digitTrainingFolder);

                //
                // testing images.
                string digitTestingFolder = Path.Combine(trainingFolder, digit);

                if (!Directory.Exists(digitTestingFolder))
                    continue;

                var testingImages = Directory.GetFiles(digitTestingFolder);

                Directory.CreateDirectory($"{testOutputFolder}\\{digit}");

                //int counter = 0;

                string outFolder = $"{testOutputFolder}\\{digit}";

                Directory.CreateDirectory(outFolder);

                double digitDouble = int.Parse(digit);

                double parity = 18.0;

                if (digitDouble % 2 == 0)
                {
                    parity = 20.0; // Even
                }
                else
                {
                    parity = 19.0; // Odd
                }

                foreach (string image in trainingImages)
                {
                    Sample sample = new Sample();
                    var imageName = Path.GetFileName(image);

                    sample.Feature.Add("shape", Path.Combine(digitTrainingFolder, imageName));
                    sample.Feature.Add("parity", parity);
                    sample.Feature.Add("object", digitDouble);

                    trainingSamples.Add(sample);
                }

                foreach (string image in testingImages)
                {
                    Sample sample = new Sample();
                    var imageName = Path.GetFileName(image);

                    sample.Feature.Add("shape", Path.Combine(digitTestingFolder, imageName));
                    sample.Feature.Add("parity", parity);
                    sample.Feature.Add("object", digitDouble);

                    testingSamples.Add(sample);
                }
            }

            ObjectRecognition experiment = new ObjectRecognition();
            var predictor = experiment.Run(trainingSamples, testingSamples);
        }

        private static void RunMultiSimpleSequenceLearningExperiment()
        {
            Dictionary<string, List<double>> sequences = new Dictionary<string, List<double>>();

            sequences.Add("S1", new List<double>(new double[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, }));
            sequences.Add("S2", new List<double>(new double[] { 10.0, 11.0, 12.0, 13.0, 14.0, 15.0, 16.0 }));

            //
            // Prototype for building the prediction engine.
            MultiSequenceLearning experiment = new MultiSequenceLearning();
            var predictor = experiment.Run(sequences);         
        }


        /// <summary>
        /// This example demonstrates how to learn two sequences and how to use the prediction mechanism.
        /// First, two sequences are learned.
        /// Second, three short sequences with three elements each are created und used for prediction. The predictor used by experiment privides to the HTM every element of every predicting sequence.
        /// The predictor tries to predict the next element.
        /// </summary>
        private static void RunMultiSequenceLearningExperiment()
        {
            Dictionary<string, List<double>> sequences = new Dictionary<string, List<double>>();

            //sequences.Add("S1", new List<double>(new double[] { 0.0, 1.0, 0.0, 2.0, 3.0, 4.0, 5.0, 6.0, 5.0, 4.0, 3.0, 7.0, 1.0, 9.0, 12.0, 11.0, 12.0, 13.0, 14.0, 11.0, 12.0, 14.0, 5.0, 7.0, 6.0, 9.0, 3.0, 4.0, 3.0, 4.0, 3.0, 4.0 }));
            //sequences.Add("S2", new List<double>(new double[] { 0.8, 2.0, 0.0, 3.0, 3.0, 4.0, 5.0, 6.0, 5.0, 7.0, 2.0, 7.0, 1.0, 9.0, 11.0, 11.0, 10.0, 13.0, 14.0, 11.0, 7.0, 6.0, 5.0, 7.0, 6.0, 5.0, 3.0, 2.0, 3.0, 4.0, 3.0, 4.0 }));

            sequences.Add("S1", new List<double>(new double[] { 0.0, 1.0, 2.0, 3.0, 4.0, 2.0, 5.0, }));
            sequences.Add("S2", new List<double>(new double[] { 8.0, 1.0, 2.0, 9.0, 10.0, 7.0, 11.00 }));

            //
            // Prototype for building the prediction engine.
            MultiSequenceLearning experiment = new MultiSequenceLearning();
            var predictor = experiment.Run(sequences);

            //
            // These list are used to see how the prediction works.
            // Predictor is traversing the list element by element. 
            // By providing more elements to the prediction, the predictor delivers more precise result.
            var list1 = new double[] { 1.0, 2.0, 3.0, 4.0, 2.0, 5.0 };
            var list2 = new double[] { 2.0, 3.0, 4.0 };
            var list3 = new double[] { 8.0, 1.0, 2.0 };

            predictor.Reset();
            PredictNextElement(predictor, list1);

            predictor.Reset();
            PredictNextElement(predictor, list2);

            predictor.Reset();
            PredictNextElement(predictor, list3);
        }

        private static void PredictNextElement(Predictor predictor, double[] list)
        {
            Debug.WriteLine("------------------------------");

            foreach (var item in list)
            {
                var res = predictor.Predict(item);

                if (res.Count > 0)
                {
                    foreach (var pred in res)
                    {
                        Debug.WriteLine($"{pred.PredictedInput} - {pred.Similarity}");
                    }

                    var tokens = res.First().PredictedInput.Split('_');
                    var tokens2 = res.First().PredictedInput.Split('-');
                    Debug.WriteLine($"Predicted Sequence: {tokens[0]}, predicted next element {tokens2.Last()}");
                }
                else
                    Debug.WriteLine("Nothing predicted :(");
            }

            Debug.WriteLine("------------------------------");
        }
    }
}
