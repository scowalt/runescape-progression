# OSRS Wiki ingestion research

The Wiki provides structured data through Bucket. This supports an alternative to scraping rendered pages, but it does not establish a complete planning dataset or unrestricted bulk access.

This research inspected documentation only. No API query, bulk scrape, database creation, or maintainer contact took place.

## Access methods

The [OSRS Bucket guide](https://oldschool.runescape.wiki/w/RuneScape:Bucket) explicitly invites external users to query `https://oldschool.runescape.wiki/api.php?action=bucket`. The `query` parameter selects a bucket and fields. It describes the former Semantic MediaWiki `action=ask` interface as hard deprecated.

The [Bucket API documentation](https://meta.weirdgloop.org/w/Extension:Bucket/Api) describes the query interface. The [field inventory](https://oldschool.runescape.wiki/w/Help:Editing/Bucket) lists available buckets, fields, and types. A rendered wiki table does not necessarily have an equivalent normalized representation.

The [AutoWikiBrowser documentation](https://oldschool.runescape.wiki/w/RuneScape:AutoWikiBrowser) references `Special:Export`. This research did not establish a maintained full OSRS dump, a Bucket database snapshot, or a dump publication schedule.

## Automated access boundaries

The [Terms of Use](https://weirdgloop.org/terms/), updated 31 January 2025, prohibit automated use without prior consent and activity that stresses the infrastructure. The Bucket guide separately invites external queries. The inspected documents do not explain whether that invitation covers this project's intended autonomous ingestion volume and behavior.

Clarify the permitted scope before broad automated ingestion. This is a project recommendation, not a finding that every documented API query requires separate permission.

The published [robots.txt](https://oldschool.runescape.wiki/robots.txt) disallows API URLs and several namespaces for generic crawlers. It also excludes several named AI crawlers. Those directives coexist with the API documentation. Do not treat an accessible endpoint as permission to ignore crawler restrictions.

No current numerical Bucket or MediaWiki read-request allowance was verified. An [informal 2018 discussion](https://oldschool.runescape.wiki/w/Forum:API_and_terms_of_use) recommends client identification and reasonable volume, but it predates the current terms.

The [real-time prices API](https://oldschool.runescape.wiki/w/RuneScape:Real-time_Prices) has separate client-identification and rate guidance. Do not apply that guidance as a Bucket request allowance.

[MediaWiki API etiquette](https://www.mediawiki.org/wiki/API:Etiquette) recommends identifiable clients, serialized and batched requests, caching, read-only GET requests, `maxlag`, and backoff. These are general operating recommendations, not an OSRS-specific access grant. [Operator commentary](https://weirdgloop.org/blog/clankers) explains the load caused by indiscriminate traversal of links and expensive page variants.

## Structured data coverage

The following findings come from the [published field inventory](https://oldschool.runescape.wiki/w/Help:Editing/Bucket). They describe representation, not verified completeness or correctness.

- Item records expose identifiers, names, membership, values, weight, and variants. An identifier field can contain text or multiple values. One page is not necessarily one item variant.
- Recipes expose production JSON and links to materials, skills, tools, and facilities. Membership and boostability fields also exist. The completeness of quantities, durations, exceptions, and access rules remains unverified.
- Drop tables expose JSON, quantities, rolls, rarity, and approximation fields. Some rarity data is text. Conditional tables and special rolls need faithful interpretation.
- Locations expose coordinates, planes, and map identifiers. These do not establish traversable routes, access conditions, transport availability, or journey duration.
- Quest records expose requirements, required items, start points, Ironman concerns, and JSON. Major fields remain text rather than fully normalized prerequisite logic.

No documented complete travel graph or universally populated action-duration dataset was established. A quest's descriptive length is not a measured completion time. Narrative exceptions, such as collection before acceptance and partial hand-ins, still need explicit interpretation.

## Reuse and attribution

The [licensing page](https://meta.weirdgloop.org/w/Licensing) specifies CC BY-NC-SA 3.0 for OSRS article revisions from 1 October 2018 onward, subject to stated exceptions. Older revisions have different terms. Attribution can use links to the reused source articles.

The [Creative Commons summary](https://creativecommons.org/licenses/by-nc-sa/3.0/) describes attribution, license links, indications of changes, noncommercial use, and ShareAlike obligations for distributed adaptations. Media files require separate license checks. Do not assume the article-text license covers game artwork or screenshots.

No blanket CC0 declaration for Bucket data was verified. These documents do not conclusively classify a particular facts-only mechanics database as protected reuse or an adaptation. Converting text to JSON or paraphrasing it with an LLM does not automatically remove applicable obligations. Reuse rules need review before public distribution or commercial plans.

## Implications for the proposed importer

Prefer deterministic mapping where structured fields preserve the needed meaning. Reserve LLM interpretation for narrative rules, exceptions, and gaps. This is a recommendation, not an approved implementation choice.

Keep the project-owned model separate from the Wiki's transport and page structure. A source reference identifies evidence but does not prove a record correct. A record can preserve valid syntax while losing a condition, quantity, account restriction, or intermediate state.

Potential import records need traceable source references and a distinction between sourced mechanics and timing estimates. The later interview settled initial operator approval and allowed explicit timing estimates. Current product decisions are recorded in [the design notes](../design-notes.md). Automated acceptance gates remain future work.

## Evidence boundaries

The parent agent directly reviewed the OSRS Bucket guide, current Terms of Use, and licensing page. The research agent inspected the additional documentation cited above. Its ID is `68b34ed9-2b37-4680-8926-adc1f45d119d`.

Unresolved points include bulk-access permission, request-rate expectations, dump availability, field completeness, actual endpoint behavior, timing coverage, and the legal classification of the eventual dataset. This research does not settle those points through assumptions.
