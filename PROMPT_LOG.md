### Log of Prompts

Prompt: "I'm going to do this take-home assessment, and I first want to lay out some ground rules for our sessions. Save this in CLAUDE.md:
1. Never add, commit, or push any changes. I will do that manual work.
2. Never expose any API keys. .env and .env.local are already in .gitignore, but as an additional safeguard do not ever expose any API keys. Do not ever include any API keys in thinking text, output, comments, or code at all.
3. When you make pivotal changes, check for issues & bugs using eslint and npm run build. If you find any issues, fix them immediately before moving onto the next task.
4. Before making a pivotal change, go into Plan mode so we can collaborate and think through what is going to be done.
5. Utilize subagents to get more done quicker.
6. Important: After every prompt I send you, add it to PROMPTS_LOG.md and use the provided template. Do not fill out reasoning and adjustments. Just put the prompt in quotations and leave a space after every set of {prompt, reasoning, adjustment}.

Now, about the project. I want to use .NET 10 for the backend and React for the frontend. I want you to create a spec (IMPLEMENTATION_SPEC.md) for a 5-phase implementation of daily-stock-summary, phases 1-2 being for the backend, 3-4 for the frontend, and 5 for fixes, testing, and deployment. Make each phase have real, concrete milestones that we can test before moving onto the next phase. The spec will be the source of truth whenever a new conversation is started, an existing one is compacted, or another agent joins the equation. So, make sure it gets updated with what is completed (ex. cross out the phases after they are complete) and if development is stopped midway through a phase make sure there is a note about what has been done and what needs to be done.

For more on the frontend, I'd like invalid symbols/failed requests/any other notifications to be a toast notification that pops up in a corner of the app. Take-Home Assessment.pdf says that the user should be able to enter a stock symbol and meaningfully view the data, but I think we can build on top of that and make it so you can have multiple stock symbols that you can view, with multiple graphs. I'd like to do a graph view for each stock and a table view for each stock. If you have multiple graphs on the dashboard, I'd like it so that you can rearrange them fluidly by dragging on a top bar or something. We can iterate on the frontend once we are there but that is a general idea I'd like to maintain.

I have added a skill to .claude/skills: 
test-driven-development: this should be used primarily for backend development, for testing API logic and other backend features. Do not use it for frontend development.

I am going to use commands after I send this prompt to add frontend-design and pr-review-toolkit to improve efficiency. Ready to get started."
Reasoning: I wanted to create a source of truth for Claude Code to go back to any time it had a design question, and creating an implementation spec was perfect for that. The prompt contains detailed instructions for my tech stack (.NET 10 and React), general requirements for quality of output, and even a suggestion to help me fill out this file, PROMPT_LOG.md.
Adjustments: None

Prompt: "I addressed #1 and #2. Check #2. I just renamed it to PROMPT_LOG.md, thanks for checking that. What do you mean for #4?"
Reasoning: Claude gave me a list of initial improvements to get into before beginning phase 1. One of the issues was about a file name mismatch.
Adjustments: None

Prompt: "Let's do ascending, because we'll have most recent days at the top of the stock history table and then at the right of graphs."
Reasoning: Having the most recent days at the top of the stock history table is key for providing a good user experience; stock purchasing/selling decisions happen primarily on recent data.
Adjustments: Claude asked a series of questions, including how stock tables should look (ascending vs. descending days). Answer is given in prompt.

Prompt: "I installed the .NET 10 SDK. Verify it and let's go into planning mode for Phase 1."
Reasoning: I had not yet installed the .NET 10 SDK on my laptop. After it was installed, everything was ready to begin phase 1.
Adjustments: None

Prompt: "Good job with phase 1! I want to confirm that you are writing high quality tests. Don't just write tests that do assertions like result.Should().NotBeEmpty(), test something real that happens through actual calculations or processes. Test for real, tangible scenarios, not fake scenarios made up to have tests pass and to get coverage. So review your phase 1 tests for this, and if you have no changes to make, go ahead to phase 2."
Reasoning: Upon inspection of Claude's created tests, I found some low quality ones. Thus, I found it necessary to reinforce good test quality for the recently created tests and future ones as well.
Adjustments: None

Prompt: "go"
Reasoning: This was after Claude fixed the low quality tests, and it was to begin phase 2. After phase 2 completed, Claude ran the 178 tests on its own, but I verified it by building the dotnet project and running 'dotnet test'. Moreover, I started the backend server and used curl to hit the GET /api/stocks/{symbol}/daily-summary endpoint for both valid 200 returns and error code returns.
Adjustments: Kept after testing verified successful completion of phases 1-2.

Prompt: "/compact summarize this conversation and what to do next for a new claude code session and put the summary into a temp text file in the repo root called temp.txt"
Reasoning: I compacted the session window to save tokens and also to move the claude code session from my laptop to my desktop computer, and providing a description after the /compact command was the perfect way to direct a summary into a medium that will transport to my desktop.
Adjustments: None

Prompt: "Read temp.txt and IMPLEMENTATION_SPEC.md and any other context you need. Go for phase 3. Use the frontend-design skill"
Reasoning: After moving to desktop, gave Claude the necessary instructions to begin a new conversation. Told it to use frontend-design skill to make a good UI for the frontend.
Adjustments: None

Prompt: "Good job. Go ahead and begin phase 4 and resolve any remaining things to do until phase 5"
Reasoning: Phase 3 worked with no issues from eslint, npm run build, or anything really. Further frontend testing could not be done until phase 4 was complete.
Adjustments: None

Prompt: "Go for phase 5"
Reasoning: I tested phase 4's result by running the backend in one powershell terminal via dotnet run, and then the frontend in another terminal via npm run dev. The frontend was then accessible on localhost, where I was able to successfully test the daily stock summary capabilities, including both chart and table views, multiple stock symbols viewable, and fluid, adjustable layouts. Phase 5 is the last round, consisting of general improvements and cleanup where needed.
Adjustments: None
