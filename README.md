<p align="center">
<img width="200" height="200" alt="We did it! See that, goro... If we put our heads together... anything is possible!" src="https://github.com/way-of-doing/goro/blob/main/assets/images/yunobo.png" />
</p>

<h1 align="center">What's going on here, goro?!</h1>
<h3 align="center">A music library query and audit tool, with learning experiences on top</h3>

-----

Goro is meant to be a practically useful music library management tool -- one that scratches my own itch, but also goes further than that in many ways.

What goro strives to be sits at the intersection of several lines: **a practically useful tool**; **a learning experience** for me; **a good example** for others; and of course, **a source of joy**. Discovering the joy in whatever you do is the way, goro.
 
### Current status

Goro is under heavy development. Lots of things have been specified and implemented, but also lots of things, important things that an 1.0 release needs, aren't there yet. So there's no feature list yet -- some things work, but nothing yet is complete as initially imagined.

What _is_ there already?
 - [`list`](docs/commands/list.md) and [`hash`](docs/commands/hash.md) commands
 - a five-stage compiler for [predicates](docs/concepts/predicates.md), and the associated runtime
 - [documentation](docs/) and specs for things that have been or will be implemented
 
And, perhaps most importantly: [exported logs](docs/sessions/) of me and Claude working. You get to see not only the result and how it was built, but also exactly how it was planned.
  
### Development process

1. I write an initial specification and other documents to kick things off, or we discuss a topic with Claude and it then writes a brief accordingly
2. Specifications and architecture are extensively discussed with Claude; we revise until reaching something I'm happy with
3. Claude implements business code and tests; I do a high-level review
4. The results, including an exported log of the conversation with Claude, are committed
5. Whenever it makes sense, I focus on something for a detailed review and potential "righting of the ship" -- without Claude
