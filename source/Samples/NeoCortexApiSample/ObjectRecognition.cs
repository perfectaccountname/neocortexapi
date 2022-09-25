using HtmImageEncoder;
using NeoCortex;
using NeoCortexApi;
using NeoCortexApi.Classifiers;
using NeoCortexApi.Encoders;
using NeoCortexApi.Entities;
using NeoCortexApi.Network;
using NeoCortexApi.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NeoCortexApiSample
{
    /// <summary>
    /// Implements an experiment that demonstrates how to learn sequences.
    /// </summary>
    public class ObjectRecognition
    {
        /// <summary>
        /// Runs the learning of sequences.
        /// </summary>
        /// <param name="trainingSamples">Dictionary of training samples. KEY is the feature's name, the VALUE is the value of the feature.</param>
        /// <param name="testingSamples">Dictionary of testing samples. KEY is the feature's name, the VALUE is the value of the feature.</param>
        public Predictor Run(List<Sample> trainingSamples, List<Sample> testingSamples)
        {
            Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(ObjectRecognition)}");

            int inputBits = 784;
            int numColumns = 1024;

            HtmConfig cfg = new HtmConfig(new int[] { inputBits }, new int[] { numColumns })
            {
                Random = new ThreadSafeRandom(42),

                CellsPerColumn = 25,
                GlobalInhibition = true,
                LocalAreaDensity = -1,
                NumActiveColumnsPerInhArea = 0.02 * numColumns,
                PotentialRadius = (int)(0.15 * inputBits),
                //InhibitionRadius = 15,

                MaxBoost = 10.0,
                DutyCyclePeriod = 25,
                MinPctOverlapDutyCycles = 0.75,
                MaxSynapsesPerSegment = (int)(0.02 * numColumns),

                ActivationThreshold = 15,
                ConnectedPermanence = 0.5,

                // Learning is slower than forgetting in this case.
                PermanenceDecrement = 0.25,
                PermanenceIncrement = 0.15,

                // Used by punishing of segments.
                PredictedSegmentDecrement = 0.1
            };

            double max = 20;

            Dictionary<string, object> settings = new Dictionary<string, object>()
            {
                { "W", 15},
                { "N", inputBits},
                { "Radius", -1.0},
                { "MinVal", 0.0},
                { "Periodic", false},
                { "Name", "scalar"},
                { "ClipInput", false},
                { "MaxVal", max}
            };

            // IMAGE ENCODER
            ImageEncoder imgEncoder = new(new Daenet.ImageBinarizerLib.Entities.BinarizerParams()
            {
                Inverse = false,
                ImageHeight = 28,
                ImageWidth = 28,
                GreyScale = true,
            });

            EncoderBase encoderScalar = new ScalarEncoder(settings);

            return RunExperiment(cfg, encoderScalar, imgEncoder, trainingSamples, testingSamples);
        }

        /// <summary>
        ///
        /// </summary>
        private Predictor RunExperiment(HtmConfig cfg, EncoderBase encoderScalar, EncoderBase encoderImage, List<Sample> trainingSamples, List<Sample> testingSamples)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var mem = new Connections(cfg);

            bool isInStableState = false;

            HtmClassifier<string, ComputeCycle> cls = new HtmClassifier<string, ComputeCycle>();

            var numUniqueInputs = trainingSamples.Count;

            CortexLayer<object, object> layer1 = new CortexLayer<object, object>("L1");

            CortexLayer<object, object> layer2 = new CortexLayer<object, object>("L1");

            CortexLayer<object, object> layer3 = new CortexLayer<object, object>("L3");

            //TemporalMemory tm = new TemporalMemory();

            // For more information see following paper: https://www.scitepress.org/Papers/2021/103142/103142.pdf
            HomeostaticPlasticityController hpc = new HomeostaticPlasticityController(mem, numUniqueInputs * 50, (isStable, numPatterns, actColAvg, seenInputs) =>
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
            }, numOfCyclesToWaitOnChange: 50);


            SpatialPooler sp = new SpatialPooler(hpc);
            sp.Init(mem);
            //tm.Init(mem);

            layer1.HtmModules.Add("encoder", encoderImage);
            layer1.HtmModules.Add("sp", sp);

            layer2.HtmModules.Add("encoder", encoderScalar);
            layer2.HtmModules.Add("sp", sp);

            layer3.HtmModules.Add("encoder", encoderScalar);
            layer3.HtmModules.Add("sp", sp);

            //double[] inputs = inputValues.ToArray();
            int[] prevActiveCols = new int[0];
            
            int cycle = 0;
            int matches = 0;

            var lastPredictedValues = new List<string>(new string[] { "0"});
            
            int maxCycles = 3500;

            //
            // Training SP to get stable. New-born stage.
            //
            for (int i = 0; i < maxCycles && isInStableState == false; i++)
            {
                matches = 0;

                cycle++;

                Debug.WriteLine($"-------------- Newborn Cycle {cycle} ---------------");

                foreach (var sample in trainingSamples)
                {
                    var lyrOut1 = layer1.Compute(sample.Feature["shape"], true);

                    var lyrOut2 = layer2.Compute(sample.Feature["parity"], true);

                    var lyrOut3 = layer3.Compute(sample.Feature["object"], true);

                    if (isInStableState)
                        break;
                }
            }

            // Clear all learned patterns in the classifier.
            cls.ClearState();

            foreach (var sample in trainingSamples)
            {
                var lyrOut1 = layer1.Compute(sample.Feature["shape"], false);
                var activeColumns = layer1.GetResult("sp") as int[];

                var lyrOut3 = layer3.Compute(sample.Feature["object"], false);
                var activeObjColumns = layer3.GetResult("sp") as int[];

                cls.LearnObj(activeColumns, activeObjColumns);

                var lyrOut2 = layer2.Compute(sample.Feature["parity"], false);
                activeColumns = layer2.GetResult("sp") as int[];

                cls.LearnObj(activeColumns, activeObjColumns);
            }         

            foreach (var sample in testingSamples)
            {
                var lyrOutTest = layer1.Compute(sample.Feature["shape"], false);
                var actColumns = layer1.GetResult("sp") as int[];
                var predictedObj = cls.GetPredictedObj(actColumns);

                cls.ResetSelectedObjs();

                lyrOutTest = layer2.Compute(sample.Feature["parity"].ToString(), false);
                actColumns = layer2.GetResult("sp") as int[];
                predictedObj = cls.GetPredictedObj(actColumns);

                cls.ResetSelectedObjs();
            }

            //var lyrOutTest = layer1.Compute(trainingSamples[0].Feature["shape"], false);
            //var actColumns = layer1.GetResult("sp") as int[];
            //var predictedObj = cls.GetPredictedObj(actColumns);

            //cls.ResetSelectedObjs();

            //lyrOutTest = layer2.Compute(trainingSamples[1].Feature["parity"].ToString(), false);
            //actColumns = layer2.GetResult("sp") as int[];
            //predictedObj = cls.GetPredictedObj(actColumns);

            //cls.ResetSelectedObjs();

            //lyrOutTest = layer2.Compute(trainingSamples[2].Feature["parity"].ToString(), false);
            //actColumns = layer2.GetResult("sp") as int[];
            //predictedObj = cls.GetPredictedObj(actColumns);

            Debug.WriteLine("------------ END ------------");

            return new Predictor(layer1, mem, cls);
        }


        /// <summary>
        /// Constracts the unique key of the element of an sequece. This key is used as input for HtmClassifier.
        /// It makes sure that alle elements that belong to the same sequence are prefixed with the sequence.
        /// The prediction code can then extract the sequence prefix to the predicted element.
        /// </summary>
        /// <param name="prevInputs"></param>
        /// <param name="input"></param>
        /// <param name="sequence"></param>
        /// <returns></returns>
        private static string GetKey(List<string> prevInputs, double input, string sequence)
        {
            string key = String.Empty;

            for (int i = 0; i < prevInputs.Count; i++)
            {
                if (i > 0)
                    key += "-";

                key += (prevInputs[i]);
            }

            return $"{sequence}_{key}";
        }
    }
}
