namespace FireAnt

open FireAnt.Transport
open System.IO
open System.Collections.Generic

type IWorkspaceBuilder =
    abstract member Build: string -> FileInfo

type ITestTimeRepository =
    abstract member GetPredicted: TestCase -> decimal option

type ISplitStrategy =
    abstract member Split: IReadOnlyList<(TestCase * decimal option)> -> IReadOnlyList<IReadOnlyList<TestCase>>