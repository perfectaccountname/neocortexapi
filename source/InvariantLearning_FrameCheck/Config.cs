﻿using NeoCortexApi.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvariantLearning_FrameCheck
{

    /// <summary>
    /// Experiment Folder Configuration
    /// </summary>
    public class ExperimentConfig
    {
        /// <summary>
        /// Path to the the folder which contains the training images
        /// </summary>
        public string? PathToTrainDataFolder { get; set; }

        /// <summary>
        /// Path to the folder which contains the testing image
        /// </summary>
        public string? PathToTestDataFolder { get; set; }

        /// <summary>
        /// Run Parameter of the Experiment
        /// </summary>
        public RunConfig? runParams { get; set; }

        /// <summary>
        /// The Folder path which contains the experiment
        /// </summary>
        public string? ExperimentFolder { get; set; }
    }

    /// <summary>
    /// Experiment running configuration
    /// </summary>
    public class RunConfig
    {
        /// <summary>
        /// Hierarchical Temporal Memory Configuration
        /// </summary>
        public HtmConfig? htmConfig { get; set; }

        /// <summary>
        /// Iteration through whole dataset
        /// </summary>
        public int Epoch { get; set; }
    }
}
