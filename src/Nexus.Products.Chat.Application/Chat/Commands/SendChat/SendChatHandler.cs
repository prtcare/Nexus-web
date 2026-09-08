using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Nexus.Intelligence.Contracts;
using Nexus.Products.Chat.Application.Chat.Context;
using Nexus.Products.Chat.Application.Chat.Identity;
using Nexus.Products.Chat.Application.Knowledge.Services;
using Nexus.Products.Chat.Domain.Adr;
using Nexus.Products.Chat.Domain.Conversation;
using Nexus.Products.Chat.Domain.ConversationMessage;
using Nexus.Products.Chat.Domain.Knowledge;
using Nexus.ProductCore.Scope.Project;
using ChatProjectId = Nexus.Products.Chat.Domain.Project.ProjectId;
using ScopeProjectId = Nexus.ProductCore.Scope.Common.Identifiers.ProjectId;
using ChatWorkspaceId = Nexus.Products.Chat.Domain.Common.Identifiers.WorkspaceId;
using Nexus.Products.Chat.Domain.WorkItem;

namespace Nexus.Products.Chat.Application.Chat.Commands.SendChat;

public sealed class SendChatHandler
{
    private static readonly WorkItemStatus[] OpenWorkItemStatuses =
    [
        WorkItemStatus.New,
        WorkItemStatus.Active,
        WorkItemStatus.Blocked
    ];

    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationMessageRepository _messageRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IKnowledgeRetrievalService _knowledgeRetrievalService;
    private readonly IKnowledgeRepository _knowledgeRepository;
    private readonly IAdrRepository _adrRepository;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IChatContextBundleMapper _contextBundleMapper;
    private readonly IIntelligenceClient _intelligenceClient;
    private readonly IChatTurnIdentity _turnIdentity;
    private readonly ILogger<SendChatHandler> _logger;

    public SendChatHandler(
        IConversationRepository conversationRepository,
        IConversationMessageRepository messageRepository,
        IProjectRepository projectRepository,
        IKnowledgeRetrievalService knowledgeRetrievalService,
        IKnowledgeRepository knowledgeRepository,
        IAdrRepository adrRepository,
        IWorkItemRepository workItemRepository,
        IChatContextBundleMapper contextBundleMapper,
        IIntelligenceClient intelligenceClient,
        IChatTurnIdentity turnIdentity,
        ILogger<SendChatHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _projectRepository = projectRepository;
        _knowledgeRetrievalService = knowledgeRetrievalService;
        _knowledgeRepository = knowledgeRepository;
        _adrRepository = adrRepository;
        _workItemRepository = workItemRepository;
        _contextBundleMapper = contextBundleMapper;
        _intelligenceClient = intelligenceClient;
        _turnIdentity = turnIdentity;
        _logger = logger;
    }

    public async Task<SendChatResult> HandleAsync(
        SendChatCommand command,
        CancellationToken cancellationToken = default)
    {
        var conversation =
            await _conversationRepository.GetAsync(
                command.ConversationId,
                cancellationToken);

        if (conversation is null)
        {
            return SendChatResult.Failure("Conversation not found.");
        }

        // conversation.ProjectId is Chat's own opaque foreign-key struct (AGENTS.md
        // boundary rule); IProjectRepository now resolves the shared Layer 06 Project
        // (CHG-20260827-003), keyed by Nexus.ProductCore.Scope's ProjectId - convert at
        // this one boundary rather than changing what Conversation stores.
        var project =
            await _projectRepository.GetAsync(
                new ScopeProjectId(conversation.ProjectId.Value),
                cancellationToken);

        if (project is null)
        {
            return SendChatResult.Failure("Project not found.");
        }

        var userMessage = new ConversationMessage(
            ConversationMessageId.New(),
            conversation.Id,
            ConversationMessageRole.User,
            command.Prompt);

        await _messageRepository.AddAsync(
            userMessage,
            cancellationToken);

        conversation.RecordMessage(
            userMessage.CreatedOn);

        await _conversationRepository.UpdateAsync(
            conversation,
            cancellationToken);

        var history =
            await _messageRepository.ListByConversationAsync(
                conversation.Id,
                cancellationToken);

        var knowledge =
            await _knowledgeRetrievalService.RetrieveAsync(
                project.WorkspaceId.Value,
                command.Prompt,
                cancellationToken);

        var decisions =
            await _adrRepository.ListByKnowledgeIdsAsync(
                knowledge.Select(k => k.Id).ToArray(),
                cancellationToken);

        // IWorkItemRepository still keys on Chat's own opaque ProjectId (WorkItem was not
        // part of the CHG-20260827-003 narrow slice) - convert back at this boundary.
        var openWorkItems =
            (await _workItemRepository.ListByProjectAsync(
                new ChatProjectId(project.Id.Value),
                cancellationToken))
            .Where(workItem => OpenWorkItemStatuses.Contains(workItem.Status))
            .ToArray();

        var contextBundle = _contextBundleMapper.Map(
            new ChatContextSources
            {
                Project = project,
                Conversation = conversation,
                History = history,
                Knowledge = knowledge,
                Decisions = decisions,
                OpenWorkItems = openWorkItems
            });

        var scope = _contextBundleMapper.BuildScope(project, conversation);

        var request = new IntelligenceTurnRequest
        {
            TenantId = _turnIdentity.TenantId,
            ProductId = _turnIdentity.ProductId,
            Scope = scope,
            Actor = _turnIdentity.BuildActor(),
            Input = new TurnInput(TurnInputKind.UserMessage, command.Prompt, []),
            Context = contextBundle,
            Constraints = TurnConstraints.Default,
            IdempotencyKey = BuildIdempotencyKey(conversation.Id, command.Prompt, userMessage.Id)
        };

        var response = await _intelligenceClient.SendTurnAsync(request, cancellationToken);

        // WU-02 turn/evidence traceability: retain the Intelligence TurnId on the
        // originating user message so every turn - including refusals/failures that never
        // persist an assistant reply - can be re-traced from the conversation to its
        // Intelligence evidence (turn trace / decisions, result report).
        if (!string.IsNullOrWhiteSpace(response.TurnId))
        {
            userMessage.AttachIntelligenceTurnId(response.TurnId);

            await _messageRepository.UpdateAsync(
                userMessage,
                cancellationToken);
        }

        await ApplyPersistenceHintsAsync(project, response.PersistenceHints, cancellationToken);

        switch (response.Outcome)
        {
            case TurnOutcomeKind.Reply:
                return await PersistReplyAsync(
                    conversation,
                    response,
                    requiresClarification: false,
                    cancellationToken);

            case TurnOutcomeKind.Clarification:
                return await PersistReplyAsync(
                    conversation,
                    response,
                    requiresClarification: true,
                    cancellationToken);

            case TurnOutcomeKind.Refusal:
            case TurnOutcomeKind.Failed:
                return SendChatResult.Failure(BuildErrorText(response.Errors));

            case TurnOutcomeKind.Plan:
            case TurnOutcomeKind.Actions:
                return SendChatResult.Failure(
                    $"Outcome '{response.Outcome}' is not yet supported by Chat.");

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(response.Outcome),
                    response.Outcome,
                    "Unmapped TurnOutcomeKind - add explicit handling.");
        }
    }

    private async Task<SendChatResult> PersistReplyAsync(
        Conversation conversation,
        IntelligenceTurnResponse response,
        bool requiresClarification,
        CancellationToken cancellationToken)
    {
        var replyText = response.Reply?.Text;

        if (string.IsNullOrWhiteSpace(replyText))
        {
            return SendChatResult.Failure(
                $"Intelligence returned {response.Outcome} without reply text.");
        }

        var assistantMessage = new ConversationMessage(
            ConversationMessageId.New(),
            conversation.Id,
            ConversationMessageRole.Assistant,
            replyText);

        // WU-02 turn/evidence traceability: bind the reply to the same Intelligence turn.
        if (!string.IsNullOrWhiteSpace(response.TurnId))
        {
            assistantMessage.AttachIntelligenceTurnId(response.TurnId);
        }

        await _messageRepository.AddAsync(
            assistantMessage,
            cancellationToken);

        conversation.RecordMessage(
            assistantMessage.CreatedOn);

        await _conversationRepository.UpdateAsync(
            conversation,
            cancellationToken);

        return new SendChatResult(
            true,
            replyText,
            null,
            requiresClarification,
            response.Citations,
            response.Usage);
    }

    private async Task ApplyPersistenceHintsAsync(
        Project project,
        IReadOnlyList<PersistenceHint> hints,
        CancellationToken cancellationToken)
    {
        foreach (var hint in hints)
        {
            if (hint.Kind != PersistenceHintKind.KnowledgeCandidate)
            {
                _logger.LogInformation(
                    "Ignoring persistence hint of kind {Kind}: {Title}",
                    hint.Kind,
                    hint.Title);
                continue;
            }

            var candidate = new Nexus.Products.Chat.Domain.Knowledge.Knowledge(
                new KnowledgeId(Guid.NewGuid()),
                new ChatWorkspaceId(project.WorkspaceId.Value),
                hint.Title,
                hint.Body,
                KnowledgeType.ExtractedKnowledge,
                DateTimeOffset.UtcNow);

            // ADR-005: knowledge proposed by Intelligence is never auto-published -
            // it always lands as Draft, pending explicit user approval.
            candidate.ChangeStatus(KnowledgeStatus.Draft);

            await _knowledgeRepository.AddAsync(candidate, cancellationToken);
        }
    }

    private static string BuildErrorText(IReadOnlyList<TurnError> errors)
    {
        if (errors.Count == 0)
        {
            return "Intelligence declined to answer.";
        }

        return string.Join(
            "; ",
            errors.Select(error => $"{error.Code}: {error.Message}"));
    }

    private static string BuildIdempotencyKey(
        ConversationId conversationId,
        string prompt,
        ConversationMessageId userMessageId)
    {
        var raw = $"{conversationId.Value}:{prompt}:{userMessageId.Value}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

        return Convert.ToHexString(hash);
    }
}
