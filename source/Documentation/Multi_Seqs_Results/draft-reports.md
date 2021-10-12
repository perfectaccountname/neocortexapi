# Requirements:

1.	Extend the experiment to learn the single sequence with Reset()
	a.	Take few sequences and learn as it is now, without Reset(). Trace out results.
	b.	Then add Reset and repeat same sequences.
	c.	Compare output.
2.	Play with the exit  condition for the stable TM
  if (maxMatchCnt >= 30)
3.	Try to learn multiple sequences.
4.	User can input numbers for prediction after learning.

## Aproach/Criterias

The tested sequences:
1st sequence: { 16.0, 17.0, 18.0, 19.0, 20.0, 19.0, 18.0, 17.0, 16.0, 15.0, 16.0, 17.0 }
2nd sequence: { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0, 12.0, 13.0, 14.0, 15.0, 16.0, 17.0, 18.0, 19.0, 20.0, 19.0, 18.0, 17.0, 16.0, 15.0, 16.0, 17.0 }

Each sequence is put inside a .csv file which is inside MyInput folder. The sequence(s) will be extracted by the program and will be put into Temporal Memory (TM) to learn.

The expected result is with any number in the sequence used as an input, the TM should predict the correct sequence from the number after the input number up to the number before the input number.

There will be two runs. One run resets the Temporal Memory every cycle while the other does not. A cycle is complete when the TM learns and predicts each number in a sequence. There will be many repeated cycles for the learning process.

The results of two runs will be compared to determined if with reset or without reset is preferable.

After learning, user can input their own numbers for prediction by putting another .csv file in MyTest folder. The learned TM should predict the sequence after that number if any.

### Without Reset:
1. Number of cycles: 1000.
2. Accuracy: accuracy = matches/input length*100.0. With matches are the number of times the predicted sequence matches with the expected sequence and input length is the lenght of the sequence.
3. Stable areas: Every cycle with accuracy of 100% is saved. The consercutive cycles with accuracy of 100% are called a stable area. There could be more than one stable area. It is preferable to have a long stable area which indicates that learning is complete.

#### Example1: "+" means accuracy of 100%, "*" means accuracy < 100%
"****************++++++++"

Number of stable areas: 1
Stable area no. 1's size: 8

#### Example2:
"****************++++++++*****++++"

Number of stable areas: 2
Stable area no. 1's size: 8
Stable area no. 2's size: 4

### With Reset:
1. Number of cycles: same as without reset.
2. Accuracy: accuracy = matches/(input length - 1)*100.0.
Reason: Reseting the TM every cycle makes the first prediction to be always incorrect.
3. Stable areas: same as without reset.

## Results

Please refer to the result files at neocortexapi/source/Documentation/Multi_Seqs_Results for detailed output.

#### Without Reset 
		1st sequence: There are 8 stable areas. Most of them have an area of 1 (only 1 cycle with accuracy of 100%), which means these areas are not true stable areas and may be the result of lucky guess. The last stable area spans from cycle 373 to 1000 2hich indicates that TM has fully learned.
		2nd sequence: There are 12 stable areas. Most of them have small size even near the end. The last stable area spans from cycle 999 to 1000 which indicates that TM has not fully learned. TM may become stable with more cycles but this shows that without reset has a slow learning rate.
		
#### With Reset 
		1st sequence: Only one atable areas spanning from cycle 40 up to 1000. The TM learns faster and more stable than without reset.
		2nd sequence: Similar as above with one big stable area. Again, the TM learns faster than without reset.
	
## Conclusion
  
With reseting the TM every learning cycle, the TM is able to learn faster than without reset. With reset, the exit condition of maxMatchCnt >= 30 should be sufficient since the stable area is much larger than 30 and even if there are small, local stable areas, they are usually very small (<30 consercutive cycles).
