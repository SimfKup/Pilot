using Ascon.Pilot.ProjectionsRepository;
using Ascon.Pilot.ClientCore.Search;
using System.Linq;
using System.Collections.Generic;
using Ascon.Pilot.Core;
using Ascon.Pilot.DataClasses;
using Ascon.Pilot.Client.Search;
using Ascon.Pilot.Common.Search;
using Ascon.Pilot.Pilot.Reports;
using DevExpress.XtraReports.Parameters;

ReportContext _context = new ReportContext();

private void PilotReport_DataSourceDemanded(object sender, EventArgs e)
{
    MessagesDetailReport.DataMember = "Messages";

    LongRunning.Start(this, () =>
    {
        var project = Parameters["Project"].Value as RObject;
        if (project == null)
            throw new ReportException("Проект не выбран");

        if (project.Type.Name == SystemTypes.SHORTCUT)
        {
            project = GetRealObject(project);
            Parameters["Project"].Value = project;
        }

        var documentTypes = _context.GetTypes().Where(x => x.HasFiles).Select(x => x.Id).ToArray();
        var objectsBuilder = QueryBuilder.CreateObjectQueryBuilder();
        objectsBuilder.Must(ObjectFields.TypeId.BeAnyOf(documentTypes));
        var documents = _context.GetObjects(objectsBuilder, project.Id)
            .Select(x => ToReportDocument(x))
            .Where(x => x != null)
            .ToList();
        DataSource = documents;
    });
}

private RObject GetRealObject(RObject shortCutObj)
{
    object idValue;
    Guid realObjId;
    shortCutObj.Attributes.TryGetValue(SystemAttributes.SHORTCUT_OBJECT_ID, out idValue);
    if (Guid.TryParse(idValue.ToString(), out realObjId))
    {
        var realObj = _context.GetObject(realObjId);
        if (!realObj.IsInRecycleBin)
            return realObj;
    }

    throw new ReportException(
        "Не удалось получить проект, на который ссылается ярлык. Возможно проект находится в корзине.");
}

private ReportDocument ToReportDocument(RObject obj)
{
    var document = new ReportDocument
    {
        Title = GetTitle(obj),
        ParentId = obj.Parent.Id,
        ParentTitle = GetTitle(obj.Parent)
    };
    var messages = _context.LoadMessages(obj.Id, DateTime.MinValue, DateTime.MaxValue, int.MaxValue);

    foreach (var chatMessage in messages)
        if (chatMessage.Type == (int)RMessageType.TextMessage)
            document.Messages.Add(ToReportMessage(chatMessage));

    return document;
}

private ReportMessage ToReportMessage(RChatMessage message)
{
    var textEditHistory = message.RelatedMessages
        .Where(x => x.Type == (int)RMessageType.EditTextMessage)
        .OrderBy(x => x.Date);

    return new ReportMessage
    {
        UserName = message.Creator.DisplayName,
        Date = message.Date,
        Text = textEditHistory.Any() ? textEditHistory.Last().Text : message.Text
    };
}

public class ReportDocument
{
    public ReportDocument()
    {
        Messages = new List<ReportMessage>();
    }

    public string Title { get; set; }
    public Guid ParentId { get; set; }
    public string ParentTitle { get; set; }
    public List<ReportMessage> Messages { get; set; }
}

public class ReportMessage
{
    public string UserName { get; set; }
    public DateTime Date { get; set; }
    public string Text { get; set; }
}


private string GetTitle(RObject obj)
{
    return string.Format("{0}({1})", obj.Title, obj.Type.Title);
}