namespace KokonoeAssistant.Services
{
    public static class KokoLlmStreamingPolicy
    {
        // Used to fire as soon as completedRounds > 0 - the chat UI forced a streamed
        // text-only wrap-up the moment a SINGLE round of tool calls succeeded, regardless of
        // whether the task actually needed more steps. That made multi-step work ("list,
        // then create three folders, then move everything, then verify") stop after one
        // batch every time, reporting back and waiting for the user to say "continue" again -
        // exactly the Manus-style continuous-execution behavior the user kept asking for was
        // structurally impossible here. Background/agent callers never had this limit (they
        // already use the full round budget); this brings interactive chat in line with them.
        // maxToolRounds matches the round loop's own hard cap (round >= 22 forces no-tools
        // regardless) so this only starts mattering once that cap is genuinely close, not the
        // instant any tool succeeds. Raised from 6: a real "create 20 files" task showed the
        // active model doing one tool call per round despite being told to batch them, so a
        // repeated-action task this size needs real round budget to actually finish.
        public static bool ShouldStreamFinalAfterTools(
            bool hasChunkSink,
            int completedRounds,
            bool isImageRequest,
            bool isClaude,
            int maxToolRounds = 22)
            => hasChunkSink && completedRounds >= maxToolRounds && !isImageRequest && !isClaude;
    }
}
