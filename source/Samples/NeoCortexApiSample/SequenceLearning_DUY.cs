﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NeoCortexApi;
using NeoCortexApi.Classifiers;
using NeoCortexApi.Encoders;
using NeoCortexApi.Entities;
using NeoCortexApi.Network;
using NeoCortexApi.Utility;
using System.Globalization;


namespace NeoCortexApiSample
{
    /// <summary>
    /// Implements an experiment that demonstrates how to learn sequences.
    /// </summary>
    public class SequenceLearning_DUY
    {
        public void Run()
        {
            #region Parameters initialization for the experiment.

            Console.WriteLine($"Hello NeocortexApi! Experiment {nameof(SequenceLearning_DUY)}");

            int inputBits = 100;
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

            EncoderBase encoder = new ScalarEncoder(settings);
            #endregion

            // not stable with 2048 cols 25 cells per column and 0.02 * numColumns synapses on segment.
            // Stable with permanence decrement 0.25/ increment 0.15 and ActivationThreshold 25.
            // With increment=0.2 and decrement 0.3 has taken 15 min and didn't entered the stable state.

            //List<double> inputValues = new List<double>(new double[] { 0.0, 1.0, 0.0, 2.0, 3.0, 4.0, 5.0, 6.0, 5.0, 4.0, 3.0, 7.0, 1.0, 9.0, 12.0, 11.0, 12.0, 13.0, 14.0, 11.0, 12.0, 14.0, 5.0, 7.0, 6.0, 9.0, 3.0, 4.0, 3.0, 4.0, 3.0, 4.0 });
            //List<double> inputValues = new List<double>(new double[] { 6.0, 7.0, 9.0, 10.0 });
            //List<double> inputValues1 = new List<double>(new double[] { 15.0, 5.0, 3.0, 4.0, 20.0, 3.0, 5.0, 7.0, 4.0, 19.0, 5.0, 15.0 });

            RunExperiment(inputBits, cfg, encoder);
        }

        /// <summary>
        ///
        /// </summary>
        private void RunExperiment(int inputBits, HtmConfig cfg, EncoderBase encoder)
        {
            #region Global variables for the experiment.

            //DIRECTORIES TO STORE INPUT AND OUTPUT FILES OF THE EXPERIMENT
            //----------------INPUT FILE PATH-----------------
            string inputFolderName = "MyInput";
            string testFolderName = "MyTest";
            //------------------------------------------------

            string[] inputFileNames = Directory.GetFiles(inputFolderName, "*", SearchOption.AllDirectories);
            string[] testFileNames = Directory.GetFiles(testFolderName, "*", SearchOption.AllDirectories);

            var mem = new Connections(cfg);

            HtmClassifier<string, ComputeCycle> cls = new HtmClassifier<string, ComputeCycle>();

            CortexLayer<object, object> layer1 = new CortexLayer<object, object>("L1");

            TemporalMemory tm = new TemporalMemory();

            bool isInStableState = false;

            bool learn = true;

            // The array of bits that represents the input vector.
            List<double> inputValues = new List<double>();
            List<double[]> inputLists = new List<double[]>();
            var maxNumOfElementsInSequence = GetMaxNumElementsFromAllSequences(inputFolderName);

            inputValues = GetInputVectorFromFile(inputFileNames.First());
            double[] inputBitsOfTheSequence = inputValues.ToArray();
            int maxCycles = 300;
            int newbornCycle = 0;
            #endregion

            #region Training SP to get stable. New-born stage.

            HomeostaticPlasticityController hpa = new HomeostaticPlasticityController(mem, maxNumOfElementsInSequence * 150, (isStable, numPatterns, actColAvg, seenInputs) =>
            {
                if (isStable)
                    // Event should be fired when entering the stable state.
                    Debug.WriteLine($"STABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");
                else
                    // Ideal SP should never enter unstable state after stable state.
                    Debug.WriteLine($"INSTABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");

                // We are not learning in instable state.
                learn = isInStableState = isStable;

                // Clear all learned patterns in the classifier.
                //cls.ClearState();

            }, numOfCyclesToWaitOnChange: 50);

            SpatialPoolerMT sp = new SpatialPoolerMT(hpa);
            sp.Init(mem);
            tm.Init(mem);
            layer1.HtmModules.Add("encoder", encoder);
            layer1.HtmModules.Add("sp", sp);

            //
            // Training SP to get stable. New-born stage.
            //
            for (int i = 0; i < maxCycles; i++)
            {
                newbornCycle++;

                Debug.WriteLine($"-------------- Newborn Cycle {newbornCycle} ---------------");

                foreach (var input in inputBitsOfTheSequence)
                {
                    Debug.WriteLine($" -- {input} --");

                    var lyrOut = layer1.Compute(input, learn);

                    if (isInStableState)
                        break;
                }

                if (isInStableState)
                    break;
            }

            layer1.HtmModules.Add("tm", tm);
            #endregion

            #region Training every file in MyInput folder.

            foreach (var file in inputFileNames)
            {
                #region Variables for SP+TM training

                inputValues = GetInputVectorFromFile(file);

                List<int[]> stableAreas = new List<int[]>();

                Stopwatch sw = new Stopwatch();

                inputBitsOfTheSequence = inputValues.ToArray();
                inputLists.Add(inputBitsOfTheSequence);

                int[] prevActiveCols = new int[0];

                int cycle = 0;
                int matches = 0;

                List<string> lastPredictedValue = new List<string>();

                int maxPrevInputs = inputValues.Count - 1;
                List<string> previousInputs = new List<string>();
                //previousInputs.Add("-1.0");

                stableAreas.Clear();
                cycle = 0;
                learn = true;
                sw.Reset();
                sw.Start();
                int maxMatchCnt = 0;
                #endregion

                //
                // Now training with SP+TM. SP is pretrained on the given input pattern set.
                for (int i = 0; i < maxCycles; i++)
                {
                    #region Training with SP+TM for one cycle

                    matches = 0;

                    learn = true;

                    cycle++;

                    Debug.WriteLine($"-------------- Cycle {cycle} ---------------");

                    foreach (var input in inputBitsOfTheSequence)
                    {
                        Debug.WriteLine($"-------------- {input} ---------------");

                        var lyrOut = layer1.Compute(input, learn) as ComputeCycle;

                        // lyrOut is null when the TM is added to the layer inside of HPC callback by entering of the stable state.
                        var activeColumns = layer1.GetResult("sp") as int[];

                        previousInputs.Add(input.ToString());
                        if (previousInputs.Count > (maxPrevInputs + 1))
                            previousInputs.RemoveAt(0);

                        // In the pretrained SP with HPC, the TM will quickly learn cells for patterns
                        // In that case the starting sequence 4-5-6 might have the sam SDR as 1-2-3-4-5-6,
                        // Which will result in returning of 4-5-6 instead of 1-2-3-4-5-6.
                        // HtmClassifier allways return the first matching sequence. Because 4-5-6 will be as first
                        // memorized, it will match as the first one.
                        if (previousInputs.Count <= maxPrevInputs)
                            continue;

                        string key = GetKey(previousInputs, input);

                        List<Cell> actCells;

                        if (lyrOut.ActiveCells.Count == lyrOut.WinnerCells.Count)
                        {
                            actCells = lyrOut.ActiveCells;
                        }
                        else
                        {
                            actCells = lyrOut.WinnerCells;
                        }

                        cls.Learn(key, actCells.ToArray());

                        if (learn == false)
                            Debug.WriteLine($"Inference mode");

                        Debug.WriteLine($"Col  SDR: {Helpers.StringifyVector(lyrOut.ActivColumnIndicies)}");
                        Debug.WriteLine($"Cell SDR: {Helpers.StringifyVector(actCells.Select(c => c.Index).ToArray())}");

                        if (lastPredictedValue.Contains(key))
                        {
                            matches++;
                            Debug.WriteLine($"Match. Actual value: {key} - Predicted value: {key}");
                            lastPredictedValue.Clear();
                        }
                        else
                        {
                            Debug.WriteLine($"Mismatch! Actual value: {key} - Predicted values: {String.Join(',', lastPredictedValue)}");
                            lastPredictedValue.Clear();
                        }

                        if (lyrOut.PredictiveCells.Count > 0)
                        {
                            //var predictedInputValue = cls.GetPredictedInputValue(lyrOut.PredictiveCells.ToArray());
                            var predictedInputValues = cls.GetPredictedInputValues(lyrOut.PredictiveCells.ToArray(), 6);

                            foreach (var item in predictedInputValues)
                            {
                                Debug.WriteLine($"Current Input: {input} \t| Predicted Input: {item.PredictedInput}");
                                lastPredictedValue.Add(item.PredictedInput);
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"NO CELLS PREDICTED for next cycle.");
                            lastPredictedValue.Clear();
                        }
                    }
                    #endregion

                    #region Accuracy calculation after one cycle

                    double accuracy;
                    tm.Reset(mem);
                    //previousInputs.Clear();
                    accuracy = (double)matches / ((double)inputBitsOfTheSequence.Length - 1.0) * 100.0; // Use if with reset
                    //accuracy = (double)matches / (double)inputBitsOfTheSequence.Length * 100.0; // Use if without reset

                    Debug.WriteLine($"Cycle: {cycle}\tMatches={matches} of {inputBitsOfTheSequence.Length}\t {accuracy}%");

                    if (accuracy >= 100.0)
                    {
                        maxMatchCnt++;
                        Debug.WriteLine($"100% accuracy reached {maxMatchCnt} times.");
                        //
                        // Experiment is completed if we are 30 cycles long at the 100% accuracy.
                        //if (maxMatchCnt >= 30)
                        if (cycle == maxCycles)
                        {
                            stableAreas.Add(new int[] { cycle - maxMatchCnt, cycle });
                            sw.Stop();
                            printResults(stableAreas, sw);
                            learn = false;
                            break;
                        }
                    }
                    else if (maxMatchCnt > 0)
                    {
                        Debug.WriteLine($"At 100% accuracy after {maxMatchCnt} repeats we get a drop of accuracy with {accuracy}. This indicates instable state. Learning will be continued.");
                        stableAreas.Add(new int[] { cycle - maxMatchCnt, cycle - 1 });
                        if (cycle == maxCycles)
                        {
                            sw.Stop();
                            printResults(stableAreas, sw);
                            learn = false;
                            break;
                        }
                        maxMatchCnt = 0;
                    }
                    #endregion
                }
                Debug.WriteLine("------------ END ------------");
                previousInputs.Clear();
            }
            #endregion

            #region Prediction after training using user input in MyTest folder.
            foreach (var file in testFileNames)
            {
                userInputPrediction(layer1, cls, file, inputLists);
            }
            #endregion
        }

        #region Various helping methods created for the experiment.

        /// <summary>
        /// Print the results after learning.
        /// </summary>
        /// <param name="stableAreas">List of stable areas.</param>
        /// <param name="sw">Stop watch.</param>
        private static void printResults(List<int[]> stableAreas, Stopwatch sw)
        {
            Debug.WriteLine($"Exit experiment in the stable state after 30 repeats with 100% of accuracy. Elapsed time: {sw.ElapsedMilliseconds / 1000 / 60} min.");
            Console.WriteLine($"Exit experiment in the stable state after 30 repeats with 100% of accuracy. Elapsed time: {sw.ElapsedMilliseconds / 1000 / 60} min.");
            foreach (int[] stableArea in stableAreas)
            {
                Debug.WriteLine($"----------------Stable area number: {stableAreas.IndexOf(stableArea)}----------------");
                Console.WriteLine($"----------------Stable area number: {stableAreas.IndexOf(stableArea)}----------------");
                Debug.WriteLine($"Starting cycle: {stableArea.Min()}");
                Console.WriteLine($"Starting cycle: {stableArea.Min()}");
                Debug.WriteLine($"Ending cycle: {stableArea.Max()}");
                Console.WriteLine($"Ending cycle: {stableArea.Max()}");
                Debug.WriteLine($"Stable area's size: {stableArea.Max() - stableArea.Min()}");
                Console.WriteLine($"Stable area's size: {stableArea.Max() - stableArea.Min()}");
                Debug.WriteLine($"----------------End of Stable area number: {stableAreas.IndexOf(stableArea)}----------------");
                Console.WriteLine($"----------------End of Stable area number: {stableAreas.IndexOf(stableArea)}----------------");
            }
        }

        /// <summary>
        /// Read user input from console then predict the learned sequences.
        /// </summary>
        /// <param name="layer1">The developed cortex layer.</param>
        /// <param name="cls">The learned classifier.</param>
        private void userInputPrediction(CortexLayer<object, object> layer1, HtmClassifier<string, ComputeCycle> cls, string file, List<double[]> inputLists)
        {
            List<double> userInputs = new List<double>();
            Console.WriteLine($"----------------User input test----------------");
            using (StreamReader sr = new StreamReader(file))
            {
                List<string> listValues = new List<string>();
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    var values = line.Split(',');

                    listValues.Add(values[1]);
                }
                foreach (var input in listValues)
                {
                    userInputs.Add(double.Parse(input, CultureInfo.InvariantCulture));
                }
            }
            foreach (var userInput in userInputs)
            {
                Debug.WriteLine($"                          ");
                Console.WriteLine($"                          ");
                Debug.WriteLine($"----------------User input is: {userInput}----------------");
                Console.WriteLine($"----------------User input is: {userInput}----------------");
                Debug.WriteLine($"                          ");
                Console.WriteLine($"                          ");
                List<double[]> testLists = new List<double[]>();
                List<string> stringTestLists = new List<string>();
                var lyrOut = layer1.Compute(userInput, false) as ComputeCycle;
                if (lyrOut.PredictiveCells.Count > 0)
                {
                    var predictedValues = cls.GetPredictedInputValues(lyrOut.PredictiveCells.ToArray(), 6);
                    foreach (var inputList in inputLists)
                    {
                        for (var i = 0; i < inputList.Length; i++)
                        {
                            if (inputList[i] == userInput)
                            {
                                double[] tmp = new double[inputList.Length];
                                for (var x = 0; x < inputList.Length; x++)
                                {
                                    tmp[(x + inputList.Length + (inputList.Length - i - 2) % inputList.Length) % tmp.Length] = inputList[x];
                                }
                                testLists.Add(tmp);
                            }
                        }
                    }
                    foreach (var list in testLists)
                    {
                        foreach (var item in list)
                        {
                            stringTestLists.Add(item.ToString());
                        }
                        string key = GetKey(stringTestLists, 0);
                        foreach (var value in predictedValues)
                        {
                            Debug.WriteLine($"                          ");
                            Console.WriteLine($"                          ");

                            //printUserInputResults(predictedValues, value);

                            Debug.WriteLine($"                          ");
                            Console.WriteLine($"                          ");
                        }
                        stringTestLists.Clear();
                    }
                    predictedValues.Clear();
                }
                testLists.Clear();
                Debug.WriteLine($"                          ");
                Console.WriteLine($"                          ");
                Debug.WriteLine($"----------------END----------------");
                Console.WriteLine($"----------------END----------------");
                Debug.WriteLine($"                          ");
                Console.WriteLine($"                          ");
            }
        }

        /// <summary>
        /// Print the results after user inputs a number.
        /// </summary>
        /// <param name="predictedValues">The predicted values.</param>
        /// /// <param name="value">One value in the predicted values.</param>
        /// <returns>The maximum number of elements in all sequences in the folder.</returns>
        //private static void printUserInputResults(List<HtmClassifier<string, ComputeCycle>.ClassifierResult> predictedValues, HtmClassifier<string, ComputeCycle>.ClassifierResult value)
        //{
        //    Debug.WriteLine($"-----Predicted value number {predictedValues.IndexOf(value)} is: {value.PredictedInput}----");
        //    Console.WriteLine($"-----Predicted value number {predictedValues.IndexOf(value)} is: {value.PredictedInput}----");
        //    Debug.WriteLine($"----------------Similarity: {value.Similarity}----------------");
        //    Console.WriteLine($"----------------Similarity: {value.Similarity}----------------");
        //    Debug.WriteLine($"----------------Number of same bits: {value.NumOfSameBits}----------------");
        //    Console.WriteLine($"----------------Number of same bits: {value.NumOfSameBits}----------------");
        //}

        /// <summary>
        /// Read all sequences in files contained in the folder and calculate the maximum sequence length.
        /// </summary>
        /// <param name="folder">The folder that holds sequences used for learning.</param>
        /// <returns>The maximum number of elements in all sequences in the folder.</returns>
        private int GetMaxNumElementsFromAllSequences(string folderName)
        {
            string[] fileNames = Directory.GetFiles(folderName, "*", SearchOption.AllDirectories);
            List<double> inputValues = new List<double>();
            int numOfElementsInSequence = 0;
            int maxNumOfElementsInSequence = 0;
            foreach (var file in fileNames)
            {
                inputValues = GetInputVectorFromFile(file);
                numOfElementsInSequence = inputValues.Distinct<double>().ToList().Count;
                if (maxNumOfElementsInSequence < numOfElementsInSequence)
                {
                    maxNumOfElementsInSequence = numOfElementsInSequence;
                }
            }
            return maxNumOfElementsInSequence;
        }

        /// <summary>
        /// Run through a csv file and return the 2nd column's value as a list
        /// </summary>
        /// <param name="file">The file that holds sequences used for learning.</param>
        /// <returns>The list of values from the file.</returns>
        private List<double> GetInputVectorFromFile(string file)
        {
            Debug.WriteLine($"----------------Running file's name: {file}----------------");
            Console.WriteLine($"----------------Running file's name: {file}----------------");
            using (StreamReader sr = new StreamReader(file))
            {
                List<string> listValues = new List<string>();
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    var values = line.Split(',');

                    listValues.Add(values[1]);
                }

                List<double> inputValues = new List<double>();
                foreach (var input in listValues)
                {
                    inputValues.Add(double.Parse(input, CultureInfo.InvariantCulture));
                }

                return inputValues;
            }
        }

        private static string GetKey(List<string> prevInputs, double input)
        {
            string key = String.Empty;

            for (int i = 0; i < prevInputs.Count; i++)
            {
                if (i > 0)
                    key += "-";

                key += (prevInputs[i]);
            }

            return key;
        }
        #endregion
    }
}
