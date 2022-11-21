// Copyright (c) Damir Dobric. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
using Invariant.Entities;
using NeoCortexApi.Entities;
using NeoCortexApi.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NeoCortexApi.Classifiers
{
    /// <summary>
    /// Defines the predicting input.
    /// </summary>
    public class ClassifierResult<TIN>
    {
        /// <summary>
        /// The predicted input value.
        /// </summary>
        public TIN PredictedInput { get; set; }

        /// <summary>
        /// Number of identical non-zero bits in the SDR.
        /// </summary>
        public int NumOfSameBits { get; set; }

        /// <summary>
        /// The similarity between the SDR of  predicted cell set with the SDR of the input.
        /// </summary>
        public double Similarity { get; set; }
    }


    /// <summary>
    /// Classifier implementation which memorize all seen values.
    /// </summary>
    /// <typeparam name="TIN"></typeparam>
    /// <typeparam name="TOUT"></typeparam>
    public class HtmClassifier<TIN, TOUT> : IClassifier<TIN, TOUT>
    {
        private int maxRecordedElements = 10;

        private List<TIN> inputSequence = new List<TIN>();

        private Dictionary<int[], int> inputSequenceMap = new Dictionary<int[], int>();

        /// <summary>
        /// Recording of all SDRs. See maxRecordedElements.
        /// </summary>
        private Dictionary<TIN, List<int[]>> m_AllInputs = new Dictionary<TIN, List<int[]>>();
        private List<Sample> m_AllSamples = new List<Sample>();
        private List<Sample> m_WinnerSamples = new List<Sample>();

        /// <summary>
        /// Recording of all SDRs. See maxRecordedElements.
        /// </summary>

        private Dictionary<TIN, List<int[]>> m_SelectedInputs = new Dictionary<TIN, List<int[]>>();
        private List<Sample> m_SelectedSamples = new List<Sample>();

        /// <summary>
        /// Mapping between the input key and the SDR assootiated to the input.
        /// </summary>
        //private Dictionary<TIN, int[]> m_ActiveMap2 = new Dictionary<TIN, int[]>();

        /// <summary>
        /// Clears th elearned state.
        /// </summary>
        public void ClearState()
        {
            m_AllInputs.Clear();
        }

        /// <summary>
        /// Checks if the same SDR is already stored under the given key.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sdr"></param>
        /// <returns></returns>
        private bool ContainsSdr(TIN input, int[] sdr)
        {
            foreach (var item in m_AllInputs[input])
            {
                if (item.SequenceEqual(sdr))
                    return true;
                else
                    return false;
            }

            return false;
        }


        private int GetBestMatch(TIN input, int[] cellIndicies, out double similarity, out int[] bestSdr)
        {
            int maxSameBits = 0;
            bestSdr = new int[1];

            foreach (var sdr in m_AllInputs[input])
            {
                var numOfSameBitsPct = sdr.Intersect(cellIndicies).Count();
                if (numOfSameBitsPct >= maxSameBits)
                {
                    maxSameBits = numOfSameBitsPct;
                    bestSdr = sdr;
                }
            }

            similarity = Math.Round(MathHelpers.CalcArraySimilarity(bestSdr, cellIndicies), 2);

            return maxSameBits;
        }


        /// <summary>
        /// Assotiate specified input to the given set of predictive cells.
        /// </summary>
        /// <param name="input">Any kind of input.</param>
        /// <param name="output">The SDR of the input as calculated by SP.</param>
        public void Learn(TIN input, Cell[] output)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var cellIndicies = GetCellIndicies(output);

            Learn(input, cellIndicies);
        }

        /// <summary>
        /// Assotiate specified input to the given set of predictive cells. This can also be used to classify Spatial Pooler Columns output as int array
        /// </summary>
        /// <param name="input">Any kind of input.</param>
        /// <param name="output">The SDR of the input as calculated by SP as int array</param>
        public void Learn(TIN input, int[] cellIndicies)
        {
            if (m_AllInputs.ContainsKey(input) == false)
                m_AllInputs.Add(input, new List<int[]>());

            // Store the SDR only if it was not stored under the same key already.
            if (!ContainsSdr(input, cellIndicies))
                m_AllInputs[input].Add(cellIndicies);
            else
            {
                // for debugging
            }

            //
            // Make sure that only few last SDRs are recorded.
            if (m_AllInputs[input].Count > maxRecordedElements)
            {
                Debug.WriteLine($"The input {input} has more ");
                m_AllInputs[input].RemoveAt(0);
            }

            var previousOne = m_AllInputs[input][Math.Max(0, m_AllInputs[input].Count - 2)];

            if (!previousOne.SequenceEqual(cellIndicies))
            {
                // double numOfSameBitsPct = (double)(((double)(this.activeMap2[input].Intersect(cellIndicies).Count()) / Math.Max((double)cellIndicies.Length, this.activeMap2[input].Length)));
                // double numOfSameBitsPct = (double)(((double)(this.activeMap2[input].Intersect(cellIndicies).Count()) / (double)this.activeMap2[input].Length));
                var numOfSameBitsPct = previousOne.Intersect(cellIndicies).Count();
                Debug.WriteLine($"Prev/Now/Same={previousOne.Length}/{cellIndicies.Length}/{numOfSameBitsPct}");
            }
        }


        /// <summary>
        /// Gets multiple predicted values.
        /// </summary>
        /// <param name="predictiveCells">The current set of predictive cells.</param>
        /// <param name="howMany">The number of predections to return.</param>
        /// <returns>List of predicted values with their similarities.</returns>
        public List<ClassifierResult<TIN>> GetPredictedInputValues(Cell[] predictiveCells, short howMany = 1)
        {
            var cellIndicies = GetCellIndicies(predictiveCells);

            return GetPredictedInputValues(cellIndicies, howMany);
        }

        /// <summary>
        /// Gets multiple predicted values. This can also be used to classify Spatial Pooler Columns output as int array
        /// </summary>
        /// <param name="predictiveCells">The current set of predictive cells in int array.</param>
        /// <param name="howMany">The number of predections to return.</param>
        /// <returns>List of predicted values with their similarities.</returns>
        public List<ClassifierResult<TIN>> GetPredictedInputValues(int[] cellIndicies, short howMany = 1)
        {
            List<ClassifierResult<TIN>> res = new List<ClassifierResult<TIN>>();
            double maxSameBits = 0;
            TIN predictedValue = default;
            Dictionary<TIN, ClassifierResult<TIN>> dict = new Dictionary<TIN, ClassifierResult<TIN>>();

            var predictedList = new List<KeyValuePair<double, string>>();
            if (cellIndicies.Length != 0)
            {
                int indxOfMatchingInp = 0;
                Debug.WriteLine($"Item length: {cellIndicies.Length}\t Items: {this.m_AllInputs.Keys.Count}");
                int n = 0;

                List<int> sortedMatches = new List<int>();

                Debug.WriteLine($"Predictive cells: {cellIndicies.Length} \t {Helpers.StringifyVector(cellIndicies)}");

                foreach (var pair in this.m_AllInputs)
                {
                    if (ContainsSdr(pair.Key, cellIndicies))
                    {
                        Debug.WriteLine($">indx:{n.ToString("D3")}\tinp/len: {pair.Key}/{cellIndicies.Length}, Same Bits = {cellIndicies.Length.ToString("D3")}\t, Similarity 100.00 %\t {Helpers.StringifyVector(cellIndicies)}");

                        res.Add(new ClassifierResult<TIN> { PredictedInput = pair.Key, Similarity = (float)100.0, NumOfSameBits = cellIndicies.Length });
                    }
                    else
                    {
                        // Tried following:
                        //double numOfSameBitsPct = (double)(((double)(pair.Value.Intersect(arr).Count()) / Math.Max(arr.Length, pair.Value.Count())));
                        //double numOfSameBitsPct = (double)(((double)(pair.Value.Intersect(celIndicies).Count()) / (double)pair.Value.Length));// ;
                        double similarity;
                        int[] bestMatch;
                        var numOfSameBitsPct = GetBestMatch(pair.Key, cellIndicies, out similarity, out bestMatch);// pair.Value.Intersect(cellIndicies).Count();
                        //double simPercentage = Math.Round(MathHelpers.CalcArraySimilarity(pair.Value, cellIndicies), 2);
                        dict.Add(pair.Key, new ClassifierResult<TIN> { PredictedInput = pair.Key, NumOfSameBits = numOfSameBitsPct, Similarity = similarity });
                        predictedList.Add(new KeyValuePair<double, string>(similarity, pair.Key.ToString()));

                        if (numOfSameBitsPct > maxSameBits)
                        {
                            Debug.WriteLine($">indx:{n.ToString("D3")}\tinp/len: {pair.Key}/{bestMatch.Length}, Same Bits = {numOfSameBitsPct.ToString("D3")}\t, Similarity {similarity.ToString("000.00")} % \t {Helpers.StringifyVector(bestMatch)}");
                            maxSameBits = numOfSameBitsPct;
                            predictedValue = pair.Key;
                            indxOfMatchingInp = n;
                        }
                        else
                            Debug.WriteLine($"<indx:{n.ToString("D3")}\tinp/len: {pair.Key}/{bestMatch.Length}, Same Bits = {numOfSameBitsPct.ToString("D3")}\t, Similarity {similarity.ToString("000.00")} %\t {Helpers.StringifyVector(bestMatch)}");
                    }
                    n++;
                }
            }

            int cnt = 0;
            foreach (var keyPair in dict.Values.OrderByDescending(key => key.Similarity))
            {
                res.Add(keyPair);
                if (++cnt >= howMany)
                    break;
            }

            return res;
        }

        /// <summary>
        /// Remember the training samples.
        /// </summary>
        public void LearnObj(List<Sample> trainingSamples)
        {
            m_AllSamples.AddRange(trainingSamples);
        }

        public List<Sample> PredictObj(List<Sample> testingSamples, int howManyFeatures)
        {
            foreach (var testingSample in testingSamples)
            {
                var matchingFeatureList = GetMatchingFeatures(m_AllSamples.Select(i => i.PixelIndicies), testingSample.PixelIndicies, howManyFeatures);

                AddSelectedSamples(testingSample, matchingFeatureList);
            }
            //m_SelectedSamples = m_SelectedSamples.GroupBy(x => x.PixelIndicies).Select(y => y.First()).ToList();

            var selectedDict = m_SelectedSamples.Select(x => x).GroupBy(x => x.Object).ToDictionary(group => group.Key, group => group.ToList());

            int maxScore = 0;
            string firstWinner = "unkown";
            string secondWinner = "unkown";
            string thirdWinner = "unkown";
            Frame frame = new Frame(0, 0, 0, 0);
            Sample winnerSample = new Sample();
            foreach (var objDict in selectedDict)
            {
                int score = 0;
                for (var i = 0; i < objDict.Value.Count; i++)
                {
                    for (var j = i + 1; j < objDict.Value.Count; j++)
                    {
                        if ((Math.Abs(objDict.Value[i].Position.tlX - objDict.Value[j].Position.tlX) <= 1)
                            && (Math.Abs(objDict.Value[i].Position.tlY - objDict.Value[j].Position.tlY) <= 1))
                        {
                            score++;
                            if (score > maxScore)
                            {
                                maxScore = score;
                                secondWinner = firstWinner;
                                thirdWinner = secondWinner;
                                //if (firstWinner != objDict.Key)
                                //{
                                //    frame.tlX = 0;
                                //    frame.tlY = 0;
                                //    frame.brX = 0;
                                //    frame.brY = 0;
                                //}
                                //else
                                //{
                                //    frame.tlX = (frame.tlX + objDict.Value[j].Position.tlX) / 2;
                                //    frame.tlY = (frame.tlY + objDict.Value[j].Position.tlY) / 2;
                                //    frame.brX = (frame.brX + objDict.Value[j].Position.brX) / 2;
                                //    frame.brY = (frame.brY + objDict.Value[j].Position.brY) / 2;
                                //}
                                firstWinner = objDict.Key;
                            }
                        }
                    }
                }
                //foreach (var sampleOuter in objDict.Value)
                //{
                //    foreach (var sampleInner in objDict.Value)
                //    {
                //        int score = 0;
                //        if ((Math.Abs(sampleOuter.Position.tlX - sampleInner.Position.tlX) <= 2)
                //            && (Math.Abs(sampleOuter.Position.tlY - sampleInner.Position.tlY) <= 2))
                //        {
                //            score++;
                //            if (score > maxScore)
                //            {
                //                maxScore = score;
                //                winner = sampleInner.Object;
                //            }

                //        }
                //    }
                //}
            }
            winnerSample.Object = firstWinner;
            winnerSample.Position = frame;
            m_WinnerSamples.Add(winnerSample);

            m_SelectedSamples.Clear();

            return m_WinnerSamples;
        }

        //public void PredictObj(List<Sample> testingSamples, int howManyFeatures)
        //{
        //    var inputDict = testingSamples.Select(x => x).GroupBy(x => x.Object).ToDictionary(group => group.Key, group => group.ToList());
        //    foreach (var obj in inputDict)
        //    {
        //        foreach (var testingSample in obj.Value)
        //        {
        //            var matchingFeatureList = GetMatchingFeatures(m_AllSamples.Select(i => i.PixelIndicies), testingSample.PixelIndicies, howManyFeatures);

        //            AddSelectedSamples(testingSample, matchingFeatureList);
        //        }

        //        var selectedDict = m_SelectedSamples.Select(x => x).GroupBy(x => x.Object).ToDictionary(group => group.Key, group => group.ToList());

        //        int maxScore = 0;
        //        string winner = "unkown";
        //        foreach (var item in selectedDict)
        //        {
        //            foreach (Sample sampleOuter in item.Value)
        //            {
        //                foreach (Sample sampleInner in item.Value)
        //                {
        //                    int score = 0;
        //                    if ((Math.Abs(sampleOuter.Position.tlX - sampleInner.Position.tlX) <= 2) 
        //                        && (Math.Abs(sampleOuter.Position.tlY - sampleInner.Position.tlY) <= 2))
        //                    {
        //                        score++;
        //                        if (score > maxScore)
        //                        {
        //                            maxScore = score;
        //                            winner = sampleInner.Object;
        //                        }

        //                    }
        //                }
        //            }
        //        }
        //        var a = 1;
        //    }
        //}

        /// <summary>
        /// Find the matching samples and add them to m_SlectedSamples
        /// </summary>
        public void AddSelectedSamples(Sample testingSample, IEnumerable<int[]> matchingFeatureList)
        {
            foreach (var trainingSample in m_AllSamples)
            {
                foreach (var feature in matchingFeatureList)
                {
                    if (trainingSample.PixelIndicies.SequenceEqual(feature))
                    {
                        Sample sample = new Sample() { FramePath = "" };
                        sample.Object = trainingSample.Object;
                        sample.PixelIndicies = trainingSample.PixelIndicies;
                        sample.Position = new Frame(
                            testingSample.Position.tlX + trainingSample.Position.tlX,
                            testingSample.Position.tlY + trainingSample.Position.tlY,
                            testingSample.Position.brX + trainingSample.Position.brX,
                            testingSample.Position.brY + trainingSample.Position.brY
                        );
                        m_SelectedSamples.Add(sample);

                        //if (m_SelectedSamples.Count() > 0)
                        //{
                        //    bool isAdd = true;
                        //    foreach (var localSample in m_SelectedSamples)
                        //    {
                        //        var numOfSameBitsPct = localSample.PixelIndicies.Intersect(sample.PixelIndicies).Count();
                        //        int numOfBits = sample.PixelIndicies.Count();
                        //        double similarity = ((double)numOfSameBitsPct / (double)numOfBits) * 100;
                        //        if (similarity > 50)
                        //        {
                        //            isAdd = false;
                        //            break;
                        //        }
                        //    }
                        //    if(isAdd)
                        //    {
                        //        m_SelectedSamples.Add(sample);
                        //    }
                        //}
                        //else
                        //{
                        //    m_SelectedSamples.Add(sample);
                        //}
                    }
                }
            }
            m_SelectedSamples = m_SelectedSamples.GroupBy(x => x.PixelIndicies).Select(y => y.First()).ToList();
        }

        /// <summary>
        /// Get the best matching features with the most same bits.
        /// </summary>
        public List<int[]> GetMatchingFeatures(IEnumerable<int[]> trainingSamplesIndicies, int[] testingSamplesIndicies, int maxFeatures)
        {
            int maxSameBits = 0;
            List<int[]> results = new List<int[]>();
            foreach (var trainingIndicies in trainingSamplesIndicies)
            {
                var numOfSameBitsPct = testingSamplesIndicies.Intersect(trainingIndicies).Count();
                int numOfBits = trainingIndicies.Count();
                double similarity = ((double) numOfSameBitsPct/ (double) numOfBits)*100;
                
                if (numOfSameBitsPct >= maxSameBits /*similarity >= 50*/)
                {
                    maxSameBits = numOfSameBitsPct;
                    results.Add(trainingIndicies);
                }
            }

            //
            //Remove redundant entries.
            //if (results.Count > maxFeatures)
            //{
            //    results.RemoveRange(0, results.Count - maxFeatures);
            //}

            return results;
        }


        /// <summary>
        /// Get the best matching feature with the most same bits.
        /// </summary>
        private List<string> GetBestMatchingFeature(Dictionary<string, List<int[]>> input, int[] cellIndicies, int howMany)
        {
            int maxSameBits = 0;
            int cnt = 0;
            //string bestFeature = "";
            List<string> result = new List<string>();
            Dictionary<string, int> dict = new Dictionary<string, int>();
            foreach (var feature in input.Keys)
            {
                var inputStringArray = feature.Split("-");
                int[] inputArray = Array.ConvertAll(inputStringArray, s => int.Parse(s));
                var numOfSameBitsPct = inputArray.Intersect(cellIndicies).Count();

                if (numOfSameBitsPct >= maxSameBits)
                {
                    maxSameBits = numOfSameBitsPct;
                    //bestFeature = feature;
                    dict.Add(feature, numOfSameBitsPct);
                }
            }

            foreach (var keyPair in dict.OrderByDescending(i => i.Value))
            {
                result.Add(keyPair.Key);
                if (++cnt >= howMany)
                    break;
            }

            return result;
            //return bestFeature;
        }

        private void GetInputsFromLabel(TIN input)
        {
            if (input != null)
            {
                foreach (var pair in this.m_AllInputs)
                {
                    if (pair.Key.ToString().Contains(input.ToString()))
                    {
                        if (m_SelectedInputs.ContainsKey(pair.Key))
                        {
                            m_SelectedInputs.Remove(pair.Key);
                        }
                        m_SelectedInputs.Add(pair.Key, m_AllInputs[pair.Key]);
                    }
                }
            }
        }



        /// <summary>
        /// Gets predicted value for next cycle
        /// </summary>
        /// <param name="predictiveCells">The list of predictive cells.</param>
        /// <returns></returns>
        [Obsolete("This method will be removed in the future. Use GetPredictedInputValues instead.")]
        public TIN GetPredictedInputValue(Cell[] predictiveCells)
        {
            throw new NotImplementedException("This method will be removed in the future. Use GetPredictedInputValues instead.");
            // bool x = false;
            //double maxSameBits = 0;
            //TIN predictedValue = default;

            //if (predictiveCells.Length != 0)
            //{
            //    int indxOfMatchingInp = 0;
            //    Debug.WriteLine($"Item length: {predictiveCells.Length}\t Items: {m_ActiveMap2.Keys.Count}");
            //    int n = 0;

            //    List<int> sortedMatches = new List<int>();

            //    var celIndicies = GetCellIndicies(predictiveCells);

            //    Debug.WriteLine($"Predictive cells: {celIndicies.Length} \t {Helpers.StringifyVector(celIndicies)}");

            //    foreach (var pair in m_ActiveMap2)
            //    {
            //        if (pair.Value.SequenceEqual(celIndicies))
            //        {
            //            Debug.WriteLine($">indx:{n}\tinp/len: {pair.Key}/{pair.Value.Length}\tsimilarity 100pct\t {Helpers.StringifyVector(pair.Value)}");
            //            return pair.Key;
            //        }

            //        // Tried following:
            //        //double numOfSameBitsPct = (double)(((double)(pair.Value.Intersect(arr).Count()) / Math.Max(arr.Length, pair.Value.Count())));
            //        //double numOfSameBitsPct = (double)(((double)(pair.Value.Intersect(celIndicies).Count()) / (double)pair.Value.Length));// ;
            //        var numOfSameBitsPct = pair.Value.Intersect(celIndicies).Count();
            //        if (numOfSameBitsPct > maxSameBits)
            //        {
            //            Debug.WriteLine($">indx:{n}\tinp/len: {pair.Key}/{pair.Value.Length} = similarity {numOfSameBitsPct}\t {Helpers.StringifyVector(pair.Value)}");
            //            maxSameBits = numOfSameBitsPct;
            //            predictedValue = pair.Key;
            //            indxOfMatchingInp = n;
            //        }
            //        else
            //            Debug.WriteLine($"<indx:{n}\tinp/len: {pair.Key}/{pair.Value.Length} = similarity {numOfSameBitsPct}\t {Helpers.StringifyVector(pair.Value)}");

            //        n++;
            //    }
            //}

            //return predictedValue;
        }
        /*
        //
        // This loop peeks the best input
        foreach (var pair in this.activeMap)
        {
            //
            // We compare only outputs which are similar in the length.
            // This is important, because some outputs, which are not related to the comparing output
            // might have much mode cells (length) than the current output. With this, outputs with much more cells
            // would be declared as matching outputs even if they are not.
            if ((Math.Min(arr.Length, pair.Key.Length) / Math.Max(arr.Length, pair.Key.Length)) > 0.9)
            {
                double numOfSameBitsPct = (double)((double)(pair.Key.Intersect(arr).Count() / (double)arr.Length));
                if (numOfSameBitsPct > maxSameBits)
                {
                    Debug.WriteLine($"indx:{n}\tbits/arrbits: {pair.Key.Length}/{arr.Length}\t{pair.Value} = similarity {numOfSameBitsPct}\t {Helpers.StringifyVector(pair.Key)}");
                    maxSameBits = numOfSameBitsPct;
                    predictedValue = pair.Value;
                    indxOfMatchingInp = n;
                }

                //if (maxSameBits > 0.9)
                //{
                //    sortedMatches.Add(n);
                //    // We might have muliple matchin candidates.
                //    // For example: Let the matchin input be i1
                //    // I1 - c1, c2, c3, c4
                //    // I2 - c1, c2, c3, c4, c5, c6

                //    Debug.WriteLine($"cnt:{n}\t{pair.Value} = bits {numOfSameBitsPct}\t {Helpers.StringifyVector(pair.Key)}");
                //}
            }
            n++;
        }

        foreach (var item in sortedMatches)
        {

        }

        Debug.Write("[ ");
        for (int i = Math.Max(0, indxOfMatchingInp - 3); i < Math.Min(indxOfMatchingInp + 3, this.activeMap.Keys.Count); i++)
        {
            if (i == indxOfMatchingInp) Debug.Write("* ");
            Debug.Write($"{this.inputSequence[i]}");
            if (i == indxOfMatchingInp) Debug.Write(" *");

            Debug.Write(", ");
        }
        Debug.WriteLine(" ]");

        return predictedValue;
        //return activeMap[ComputeHash(FlatArray(output))];
    }
    return default(TIN);
    }*/


        /// <summary>
        /// Traces out all cell indicies grouped by input value.
        /// </summary>
        public string TraceState(string fileName = null)
        {
            StringWriter strSw = new StringWriter();

            StreamWriter sw = null;

            if (fileName != null)
                sw = new StreamWriter(fileName);

            List<TIN> processedValues = new List<TIN>();

            //
            // Trace out the last stored state.
            foreach (var item in this.m_AllInputs)
            {
                strSw.WriteLine("");
                strSw.WriteLine($"{item.Key}");
                strSw.WriteLine($"{Helpers.StringifyVector(item.Value.Last())}");
            }

            strSw.WriteLine("........... Cell State .............");

            foreach (var item in m_AllInputs)
            {
                strSw.WriteLine("");

                strSw.WriteLine($"{item.Key}");

                strSw.Write(Helpers.StringifySdr(new List<int[]>(item.Value)));

                //foreach (var cellState in item.Value)
                //{
                //    var str = Helpers.StringifySdr(cellState);
                //    strSw.WriteLine(str);
                //}
            }

            if (sw != null)
            {
                sw.Write(strSw.ToString());
                sw.Flush();
                sw.Close();
            }

            Debug.WriteLine(strSw.ToString());
            return strSw.ToString();
        }



        /*
    /// <summary>
    /// Traces out all cell indicies grouped by input value.
    /// </summary>
    public void TraceState2(string fileName = null)
    {

        List<TIN> processedValues = new List<TIN>();

        foreach (var item in activeMap.Values)
        {
            if (processedValues.Contains(item) == false)
            {
                StreamWriter sw = null;

                if (fileName != null)
                    sw = new StreamWriter(fileName.Replace(".csv", $"_Digit_{item}.csv"));

                Debug.WriteLine("");
                Debug.WriteLine($"{item}");

                foreach (var inp in this.activeMap.Where(i => EqualityComparer<TIN>.Default.Equals((TIN)i.Value, item)))
                {
                    Debug.WriteLine($"{Helpers.StringifyVector(inp.Key)}");

                    if (sw != null)
                        sw.WriteLine($"{Helpers.StringifyVector(inp.Key)}");
                }

                if (sw != null)
                {
                    sw.Flush();
                    sw.Close();
                }

                processedValues.Add(item);
            }
        }
    }
     */


        private string ComputeHash(byte[] rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(rawData);

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }


        private static byte[] FlatArray(Cell[] output)
        {
            byte[] arr = new byte[output.Length];
            for (int i = 0; i < output.Length; i++)
            {
                arr[i] = (byte)output[i].Index;
            }
            return arr;
        }

        private static int[] GetCellIndicies(Cell[] output)
        {
            int[] arr = new int[output.Length];
            for (int i = 0; i < output.Length; i++)
            {
                arr[i] = output[i].Index;
            }
            return arr;
        }

        private int PredictNextValue(int[] activeArr, int[] predictedArr)
        {
            var same = predictedArr.Intersect(activeArr);

            return same.Count();
        }


    }
}
