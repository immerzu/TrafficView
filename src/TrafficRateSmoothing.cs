using System;
using System.Collections.Generic;

namespace TrafficView
{
    internal static class TrafficRateSmoothing
    {
        public static void AddSample(Queue<double> samples, double value, int sampleCount)
        {
            if (samples == null)
            {
                throw new ArgumentNullException("samples");
            }

            int safeSampleCount = Math.Max(1, sampleCount);
            while (samples.Count >= safeSampleCount)
            {
                samples.Dequeue();
            }

            samples.Enqueue(Math.Max(0D, value));
        }

        public static double GetSmoothedRate(Queue<double> samples, double[] weights)
        {
            if (samples == null)
            {
                throw new ArgumentNullException("samples");
            }

            if (weights == null || weights.Length == 0)
            {
                throw new ArgumentException("At least one smoothing weight is required.", "weights");
            }

            if (samples.Count == 0)
            {
                return 0D;
            }

            double[] values = samples.ToArray();
            double weightedSum = 0D;
            double totalWeight = 0D;
            int weightOffset = Math.Max(0, weights.Length - values.Length);

            for (int i = 0; i < values.Length; i++)
            {
                int weightIndex = Math.Min(weights.Length - 1, weightOffset + i);
                double weight = Math.Max(0D, weights[weightIndex]);
                weightedSum += values[i] * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0D)
            {
                return values[values.Length - 1];
            }

            return weightedSum / totalWeight;
        }
    }
}
