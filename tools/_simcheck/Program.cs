using PES3Disc.BugReports;
var score = ReportClustering.Similarity(
    "Scan crash when inserting disc",
    "When I scan the disc the program crashes",
    "Scan crash",
    "App crashes when I scan the disc");
Console.WriteLine($"score={score:F4} threshold={BugReportLimits.ClusterSimilarityThreshold}");
