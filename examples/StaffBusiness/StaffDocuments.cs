using System.Collections.Immutable;
using IntentBlazor.Core;
using IntentBlazor.ExampleHost;

namespace IntentBlazor.StaffBusiness;

public static class StaffDocuments
{
    public static OutputContent Create(ImmutableArray<OutputDataSet> data, string message, bool canAssign) => new()
    {
        Data = data,
        Document = new()
        {
            SchemaVersion = "0.2",
            Id = "staff-business",
            RootId = "root",
            DataSets = ["jobs", "staff", "operations"],
            State = ImmutableDictionary<string, StateDefinition>.Empty.Add("selectedJob", ExampleData.Reference("jobs"))
                .Add("assignee", ExampleData.Reference("staff")).Add("notes", new() { Kind = StateKind.Text, InitialValue = JsonValues.From("") })
                .Add("recordVersion", new() { Kind = StateKind.Number, Minimum = 1, InitialValue = JsonValues.From(1) }),
            Nodes = [
                new() { Id = "root", Kind = NodeKind.Layout, Children = ["notice", "jobs", "editor", "history"] },
                ExampleData.Text("notice", "Synthetic staff workspace", ValueBinding.Constant(message + (canAssign ? " Dispatcher permission: assignment saves are enabled." : " Reader permission: local inspection/drafts are allowed; every save is denied by the server."))),
                new() { Id = "jobs", Kind = NodeKind.Table, Title = "Jobs waiting on materials", DataSetId = "jobs", SelectionState = "selectedJob", InteractionId = "chooseJob", Fields = [new("title", "Job"), new("material", "Waiting for"), new("waitingDays", "Days waiting"), new("assignee", "Assigned to"), new("version", "Version")] },
                new() { Id = "editor", Kind = NodeKind.Form, Title = "Assignment draft", WhenState = "selectedJob", Children = ["jobTitle", "currentNotes", "currentAssignee", "currentVersion", "expectedVersion", "assigneeInput", "notesInput", "save", "rebase", "clear"] },
                ExampleData.Text("jobTitle", "Selected job", ValueBinding.Selected("selectedJob", "jobs", "title")),
                ExampleData.Text("currentNotes", "Current saved notes", ValueBinding.Selected("selectedJob", "jobs", "notes")),
                ExampleData.Text("currentAssignee", "Current saved assignee", ValueBinding.Selected("selectedJob", "jobs", "assignee")),
                ExampleData.Text("currentVersion", "Current loaded record version", ValueBinding.Selected("selectedJob", "jobs", "version")),
                ExampleData.Text("expectedVersion", "Draft expected version", ValueBinding.FromState("recordVersion")),
                new() { Id = "assigneeInput", Kind = NodeKind.Select, Title = "Assign to staff", StateKey = "assignee", DataSetId = "staff", LabelField = "name" },
                new() { Id = "notesInput", Kind = NodeKind.Input, Title = "My follow-up notes", StateKey = "notes" },
                ExampleData.Button("save", "Save assignment", "save"), ExampleData.Button("rebase", "Use current record version after review", "rebase"), ExampleData.Button("clear", "Clear assignment draft", "clear"),
                new() { Id = "history", Kind = NodeKind.Table, Title = "Shared operation history", DataSetId = "operations", Fields = [new("date", "UTC"), new("actor", "Staff"), new("job", "Job"), new("description", "Operation"), new("version", "Version")] }
            ],
            Interactions = [
                new() { Id = "chooseJob", Event = UiEventKind.Select, Assignments = [new("selectedJob", ValueBinding.EventValue()), new("notes", ValueBinding.EventField("jobs", "notes")), new("assignee", ValueBinding.EventField("jobs", "assignee")), new("recordVersion", ValueBinding.EventField("jobs", "version"))] },
                new() { Id = "save", Event = UiEventKind.Activate, Action = "save_assignment", Resource = ValueBinding.FromState("selectedJob"), ExpectedVersion = ValueBinding.FromState("recordVersion"),
                    Arguments = ImmutableDictionary<string, ValueBinding>.Empty.Add("notes", ValueBinding.FromState("notes")).Add("assignee", ValueBinding.FromState("assignee")) },
                new() { Id = "rebase", Event = UiEventKind.Activate, Assignments = [new("recordVersion", ValueBinding.Selected("selectedJob", "jobs", "version"))] },
                new() { Id = "clear", Event = UiEventKind.Activate, ClearDrafts = true }
            ]
        }
    };
}
