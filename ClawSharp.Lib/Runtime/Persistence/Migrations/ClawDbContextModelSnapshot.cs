using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ClawSharp.Lib.Runtime;

[DbContext(typeof(ClawDbContext))]
internal sealed class ClawDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("ClawSharp.Lib.Runtime.MessageEntity", b =>
        {
            b.Property<string>("MessageId")
                .HasColumnType("TEXT")
                .HasColumnName("message_id")
                .HasMaxLength(128);

            b.Property<string>("BlocksJson")
                .HasColumnType("TEXT")
                .HasColumnName("blocks_json")
                .HasMaxLength(10000000);

            b.Property<string>("Content")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("content")
                .HasMaxLength(10000000);

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("TEXT")
                .HasColumnName("created_at");

            b.Property<string>("Name")
                .HasColumnType("TEXT")
                .HasColumnName("name")
                .HasMaxLength(128);

            b.Property<PromptMessageRole>("Role")
                .HasColumnType("INTEGER")
                .HasColumnName("role");

            b.Property<int>("SequenceNo")
                .HasColumnType("INTEGER")
                .HasColumnName("sequence_no");

            b.Property<string>("SessionId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("session_id")
                .HasMaxLength(128);

            b.Property<string>("ToolCallId")
                .HasColumnType("TEXT")
                .HasColumnName("tool_call_id")
                .HasMaxLength(128);

            b.Property<string>("TurnId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("turn_id")
                .HasMaxLength(128);

            b.HasKey("MessageId");

            b.HasIndex("SessionId", "SequenceNo");

            b.ToTable("messages");
        });

        modelBuilder.Entity("ClawSharp.Lib.Runtime.SessionEntity", b =>
        {
            b.Property<string>("SessionId")
                .HasColumnType("TEXT")
                .HasColumnName("session_id")
                .HasMaxLength(128);

            b.Property<string>("AgentId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("agent_id")
                .HasMaxLength(128);

            b.Property<DateTimeOffset?>("EndedAt")
                .HasColumnType("TEXT")
                .HasColumnName("ended_at");

            b.Property<DateTimeOffset>("StartedAt")
                .HasColumnType("TEXT")
                .HasColumnName("started_at");

            b.Property<SessionStatus>("Status")
                .HasColumnType("INTEGER")
                .HasColumnName("status");

            b.Property<string>("ThreadSpaceId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("thread_space_id")
                .HasMaxLength(128);

            b.Property<string>("WorkspaceRoot")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("workspace_root")
                .HasMaxLength(1024);

            b.HasKey("SessionId");

            b.HasIndex("ThreadSpaceId");

            b.ToTable("sessions");
        });

        modelBuilder.Entity("ClawSharp.Lib.Runtime.SessionEventEntity", b =>
        {
            b.Property<string>("EventId")
                .HasColumnType("TEXT")
                .HasColumnName("event_id")
                .HasMaxLength(128);

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("TEXT")
                .HasColumnName("created_at");

            b.Property<string>("EventType")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("event_type")
                .HasMaxLength(128);

            b.Property<string>("PayloadJson")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("payload")
                .HasMaxLength(10000000);

            b.Property<int>("SequenceNo")
                .HasColumnType("INTEGER")
                .HasColumnName("sequence_no");

            b.Property<string>("SessionId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("session_id")
                .HasMaxLength(128);

            b.Property<string>("TurnId")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("turn_id")
                .HasMaxLength(128);

            b.HasKey("EventId");

            b.HasIndex("SessionId", "SequenceNo");

            b.ToTable("session_events");
        });

        modelBuilder.Entity("ClawSharp.Lib.Runtime.ThreadSpaceEntity", b =>
        {
            b.Property<string>("ThreadSpaceId")
                .HasColumnType("TEXT")
                .HasColumnName("thread_space_id")
                .HasMaxLength(128);

            b.Property<DateTimeOffset?>("ArchivedAt")
                .HasColumnType("TEXT")
                .HasColumnName("archived_at");

            b.Property<string>("BoundFolderPath")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("bound_folder_path")
                .HasMaxLength(1024);

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("TEXT")
                .HasColumnName("created_at");

            b.Property<bool>("IsInit")
                .HasColumnType("INTEGER")
                .HasColumnName("is_init");

            b.Property<string>("Name")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("name")
                .HasMaxLength(256);

            b.HasKey("ThreadSpaceId");

            b.HasIndex("BoundFolderPath")
                .IsUnique();

            b.HasIndex("Name")
                .IsUnique();

            b.ToTable("thread_spaces");
        });

        modelBuilder.Entity("ClawSharp.Lib.Runtime.MessageEntity", b =>
        {
            b.HasOne("ClawSharp.Lib.Runtime.SessionEntity", null)
                .WithMany()
                .HasForeignKey("SessionId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("ClawSharp.Lib.Runtime.SessionEntity", b =>
        {
            b.HasOne("ClawSharp.Lib.Runtime.ThreadSpaceEntity", null)
                .WithMany()
                .HasForeignKey("ThreadSpaceId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        modelBuilder.Entity("ClawSharp.Lib.Runtime.SessionEventEntity", b =>
        {
            b.HasOne("ClawSharp.Lib.Runtime.SessionEntity", null)
                .WithMany()
                .HasForeignKey("SessionId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
#pragma warning restore 612, 618
    }
}
