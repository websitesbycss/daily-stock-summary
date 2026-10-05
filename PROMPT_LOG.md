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

Prompt: "Attached is a screenshot of the current dashboard if you have no stock symbols added. Using the frontend-design skill, critique the dashboard for 5 improvements we can make (in any area) and list them out to me."
Reasoning: All five phases were now complete, so it was time to move on to human testing (me) and improvements. Using multimodal inference for Claude Code was a really cool opportunity because it allowed me to just take a screenshot and give the model that context instead of describing what was going on myself.
Adjustments: None

Prompt: "For #1: I agree with raising the placeholder contrast, but the other fixes you suggested are either implemented or do not need to be changed.
For #2: The only fix I want you to implement is to show a ghosted sample panel. Do not make it too prominent, though; it should just provide a rough idea of what the user is going to get once they add a stock symbol.
For #3: I completely agree with your fix suggestions. And, I think this is an opening for us to consider a refactoring of the dashboard: when you add a symbol, it will then collapse the header into a slim top bar and also reduce the left/right padding/margin so everything becomes a sort of web app UI - think of Coinbase, Robinhood, etc. where it sort of looks like there is a lot going on but with our UX the user can handle it easily.
For #4: Instead of your suggested fixes, let us remove the daily stock summary text at the very top, but then change the Look up stocks text to Daily Stock Summary.
For #5: I agree with all your changes. I want the light mode to be very good as well.
Go ahead and implement these"
Reasoning: The frontend dashboard looked very empty and basic, so I wanted some things to change. This is where my main 'refactoring of the dashboard' suggestion came from. I also thought that users would want the option to switch between light and dark mode, not being forced to use dark mode because the app was set on that by default.
Adjustments: After the previous prompt, Claude suggested a bunch of fixes. As seen in the prompt above, I cherry-picked which ones I wanted Claude to implement and discarded the others.

Prompt: "My docker engine is taking forever to start up. Can you verify that everything works on your end?"
Reasoning: While I waited for my Docker engine to start up, I had Claude verify that things were working. Once I was able to boot it up, I tested the latest frontend changes by entering a few example stock symbols. This is where I did my main testing of the frontend, and since Claude had created over a hundred tests to verify things like edge cases and exceptions, I was not surprised when everything worked according to plan. Entering '!!!' for the stock symbol gives you a simple Toast notification in the bottom right, for example, while adding a valid symbol like TSLA almost instantaneously pulls up the chart/table view and puts it in the viewing grid.
Adjustments: Just had to restart computer to verify everything worked on the frontend.

Prompt: "Found an issue while testing: if you add multiple views to the grid and then reload the page, it starts shaking them all over the place (see image kind of), like vibrating really fast and then to stop it you have to manage to click on one of their drag bars."
Reasoning: Currently there is an issue on the grid view for all your currently added stock symbols: if you have three or more, and then you reload the page, the stock symbols will start going all over the page until you drag one of the symbol's top drag bars. Without the time constraint, this would definitely be a future fix.
Adjustments: None
