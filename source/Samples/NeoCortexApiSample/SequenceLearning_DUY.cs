using System;
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
                InhibitionRadius = 15,

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

            EncoderBase encoder = new ScalarEncoder(settings);

            // not stable with 2048 cols 25 cells per column and 0.02 * numColumns synapses on segment.
            // Stable with permanence decrement 0.25/ increment 0.15 and ActivationThreshold 25.
            // With increment=0.2 and decrement 0.3 has taken 15 min and didn't entered the stable state.

            List<double> inputValues = new List<double>();
            //List<double> inputValues = new List<double>(new double[] { 0.0, 1.0, 0.0, 2.0, 3.0, 4.0, 5.0, 6.0, 5.0, 4.0, 3.0, 7.0, 1.0, 9.0, 12.0, 11.0, 12.0, 13.0, 14.0, 11.0, 12.0, 14.0, 5.0, 7.0, 6.0, 9.0, 3.0, 4.0, 3.0, 4.0, 3.0, 4.0 });
            //List<double> inputValues = new List<double>(new double[] { 6.0, 7.0, 9.0, 10.0 });
            //List<double> inputValues1 = new List<double>(new double[] { 15.0, 5.0, 3.0, 4.0, 20.0, 3.0, 5.0, 7.0, 4.0, 19.0, 5.0, 15.0 });
            List<double> inputValues1 = new List<double>(new double[] { 1, 2, 1, 3, 2 });

            RunExperiment(inputBits, cfg, encoder, inputValues);
        }

        /// <summary>
        ///
        /// </summary>
        private void RunExperiment(int inputBits, HtmConfig cfg, EncoderBase encoder, List<double> inputValues)
        {
            //DIRECTORIES TO STORE INPUT AND OUTPUT FILES OF THE EXPERIMENT
            //----------------INPUT FILE PATH-----------------
            string folderName = "MyInput";
            //------------------------------------------------

            string[] fileNames = Directory.GetFiles(folderName, "*", SearchOption.AllDirectories);

            var mem = new Connections(cfg);

            HtmClassifier<string, ComputeCycle> cls = new HtmClassifier<string, ComputeCycle>();

            CortexLayer<object, object> layer1 = new CortexLayer<object, object>("L1");

            TemporalMemory tm = new TemporalMemory();

            bool isInStableState = false;

            Boolean withReset = true;

            Boolean firstTime = true;

            bool learn = true;

            foreach (var file in fileNames)
            {
                Debug.WriteLine($"----------------Running file's name: {file}----------------");
                Console.WriteLine($"----------------Running file's name: {file}----------------");
                using (StreamReader sr = new StreamReader(file))
                {
                    List<string> listDates = new List<string>();
                    List<string> listValues = new List<string>();
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();
                        var values = line.Split(',');

                        listDates.Add(values[0]); // Has not used these values for anything.
                        listValues.Add(values[1]);
                    }

                    foreach (var input in listValues)
                    {
                        inputValues.Add(double.Parse(input, CultureInfo.InvariantCulture));
                    }

                    List<int[]> stableAreas = new List<int[]>();

                    Stopwatch sw = new Stopwatch();
                    //sw.Start();

                    //int maxMatchCnt = 0;
                    //int globalMaxMatchCnt = 0;
                    var numInputs = inputValues.Distinct<double>().ToList().Count;
                    double[] inputs = inputValues.ToArray();

/*                    var mem = new Connections(cfg);

                    HtmClassifier<string, ComputeCycle> cls = new HtmClassifier<string, ComputeCycle>();

                    CortexLayer<object, object> layer1 = new CortexLayer<object, object>("L1");

                    TemporalMemory tm = new TemporalMemory();

                    bool isInStableState = false;*/

                    HomeostaticPlasticityController hpa = new HomeostaticPlasticityController(mem, numInputs * 150, (isStable, numPatterns, actColAvg, seenInputs) =>
                    {
                        if (isStable)
                        // Event should be fired when entering the stable state.
                        Debug.WriteLine($"STABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");
                        else
                        // Ideal SP should never enter unstable state after stable state.
                        Debug.WriteLine($"INSTABLE: Patterns: {numPatterns}, Inputs: {seenInputs}, iteration: {seenInputs / numPatterns}");

                    // We are not learning in instable state.
                    learn = isInStableState = isStable;

                    //if (isStable && layer1.HtmModules.ContainsKey("tm") == false)
                    //    layer1.HtmModules.Add("tm", tm);

                    // Clear all learned patterns in the classifier.
                    cls.ClearState();

                    // Clear active and predictive cells.
                    //tm.Reset(mem);
                    }, numOfCyclesToWaitOnChange: 50);

                    if (firstTime)
                    {
                        SpatialPoolerMT sp = new SpatialPoolerMT(hpa);
                        sp.Init(mem);
                        tm.Init(mem);
                        layer1.HtmModules.Add("encoder", encoder);
                        layer1.HtmModules.Add("sp", sp);
                    }

                    //double[] inputs = inputValues.ToArray();
                    int[] prevActiveCols = new int[0];

                    int cycle = 0;
                    int newbornCycle = 0;
                    int matches = 0;

                    List<string> lastPredictedValue = new List<string>();

                    //Dictionary<double, List<List<int>>> activeColumnsLst = new Dictionary<double, List<List<int>>>();

                    //foreach (var input in inputs)
                    //{
                    //    if (activeColumnsLst.ContainsKey(input) == false)
                    //        activeColumnsLst.Add(input, new List<List<int>>());
                    //}

                    int maxCycles = 200;
                    int maxPrevInputs = inputValues.Count - 1;
                    List<string> previousInputs = new List<string>();
                    previousInputs.Add("-1.0");

                    /*            int runTime = 2;
                                Boolean withReset = true;
                                for (int seq = 0; seq < runTime; seq++)
                                {
                                    cycle = 0;
                                    learn = true;
                                    if (seq == 0)
                                    {
                                        inputs = inputValues.ToArray();
                                    }
                                    else
                                    {
                                        inputs = inputValues1.ToArray();
                                        maxPrevInputs = inputValues1.Count - 1;
                                    }
                                    sw.Reset();
                                    sw.Start();
                                    int maxMatchCnt = 0;
                                    int globalMaxMatchCnt = 0;
                                    int globalNearMaxMatchCnt = 0;*/
                    //
                    // Training SP to get stable. New-born stage.
                    //

                    if (firstTime)
                    {
                        for (int i = 0; i < maxCycles; i++)
                        {
                            matches = 0;

                            newbornCycle++;

                            Debug.WriteLine($"-------------- Newborn Cycle {newbornCycle} ---------------");

                            foreach (var input in inputs)
                            {
                                Debug.WriteLine($" -- {input} --");

                                var lyrOut = layer1.Compute(input, learn);

                                if (isInStableState)
                                    break;
                            }

                            if (isInStableState)
                                break;
                        }
                        //if (seq == 0)
                        //{
                        layer1.HtmModules.Add("tm", tm);
                    }
                    //}

                    //TODO: Where to put this loop to learn multiple sequences??????
                    //for (int seq = 0; seq < runTime; seq++)
                    //{
                    stableAreas.Clear();
                    cycle = 0;
                    learn = true;
                    //if (runTime == 0)
                    //{
                    inputs = inputValues.ToArray();
                    //}
                    /*                    else
                                        {
                                            inputs = inputValues1.ToArray();
                                            maxPrevInputs = inputValues1.Count - 1;
                                        }*/
                    sw.Reset();
                    sw.Start();
                    int maxMatchCnt = 0;
                    //
                    // Now training with SP+TM. SP is pretrained on the given input pattern set.
                    for (int i = 0; i < maxCycles; i++)
                    {
                        matches = 0;

                        cycle++;

                        Debug.WriteLine($"-------------- Cycle {cycle} ---------------");

                        foreach (var input in inputs)
                        {
                            Debug.WriteLine($"-------------- {input} ---------------");

                            var lyrOut = layer1.Compute(input, learn) as ComputeCycle;

                            // lyrOut is null when the TM is added to the layer inside of HPC callback by entering of the stable state.
                            //if (isInStableState && lyrOut != null)
                            var activeColumns = layer1.GetResult("sp") as int[];

                            //layer2.Compute(lyrOut.WinnerCells, true);
                            //activeColumnsLst[input].Add(activeColumns.ToList());

                            previousInputs.Add(input.ToString());
                            if (previousInputs.Count > (maxPrevInputs + 1))
                                previousInputs.RemoveAt(0);

                            // In the pretrained SP with HPC, the TM will quickly learn cells for patterns
                            // In that case the starting sequence 4-5-6 might have the sam SDR as 1-2-3-4-5-6,
                            // Which will result in returning of 4-5-6 instead of 1-2-3-4-5-6.
                            // HtmClassifier allways return the first matching sequence. Because 4-5-6 will be as first
                            // memorized, it will match as the first one.
                            if (previousInputs.Count < maxPrevInputs)
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
                                var predictedInputValues = cls.GetPredictedInputValues(lyrOut.PredictiveCells.ToArray(), 3);

                                foreach (var item in predictedInputValues)
                                {
                                    Debug.WriteLine($"Current Input: {input} \t| Predicted Input: {item.PredictedInput}");
                                    //if (item.PredictedInput > 0.98)
                                    lastPredictedValue.Add(item.PredictedInput);
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"NO CELLS PREDICTED for next cycle.");
                                lastPredictedValue.Clear();
                            }
                        }
                        //TODO: The result it never reaches 100%. Why??????????
                        double accuracy;
                        if (withReset == true)
                        {
                            tm.Reset(mem);
                            accuracy = (double)matches / ((double)inputs.Length - 1.0) * 100.0;
                        }
                        else
                        {
                            accuracy = (double)matches / (double)inputs.Length * 100.0;
                        }

                        Debug.WriteLine($"Cycle: {cycle}\tMatches={matches} of {inputs.Length}\t {accuracy}%");

                        if (accuracy == 100.0)
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
                                learn = false;
                                break;
                            }
                            maxMatchCnt = 0;
                        }
                        //}
                        Debug.WriteLine("------------ END ------------");
                        firstTime = false;
                    }
                    //mode = "0";
                    //inputValues.Clear();
                    /*                while (mode != "1" && mode != "2" && mode != "3")
                                {
                                    Console.WriteLine($"Input 3 to exit or choose a mode: 1 for no reset and 2 for with reset");
                                    mode = Console.ReadLine();
                                }*/
                }
            }
            learn = false;
            tm.Reset(mem);
            var a = layer1.Compute(15, learn) as ComputeCycle;
            var predictedValues = cls.GetPredictedInputValues(a.PredictiveCells.ToArray(), 3);
            a = layer1.Compute(16, learn) as ComputeCycle;
            predictedValues = cls.GetPredictedInputValues(a.PredictiveCells.ToArray(), 3);
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

    }
}
