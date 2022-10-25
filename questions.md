# Questions

## How did you approach solving the problem?
1. Gained an understanding of the problem
2. Gained an understanding the requirments
3. Researched partial request best practice
4. Researched tools to measure internet connection speed
5. Researched off the shelve NuGet packages and libraries that offer download functionality

## How did you verify your solution works correctly?
1. Black box testing, unit tests are yet to be written but will be challenging to simulate network conditions
2. Utilised bandwidth limiting tools as suggested
3. Disconnected WiFi network mid-download
4. Reviewed requirments throughout implementation

## How long did you spend on the exercise?

Approximately 4 hours but was after a long working day, with a fresh mind and no migraine this would have likely taken less than 2 hours

## What would you add if you had more time and how?
1. In a real world scenario I would utilise an existing library ideally with an MIT license
2. I would make the download more dynamic, for example if the download is going well for the last few partial requests then maybe we can get greedy and ask for more data, on the other hand if partial requests repeatadly fail then would show mercy and ask for less data in each request
3. I would clean up the code further and make more extensible
4. Would test with large files

