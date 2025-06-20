import * as z from "zod";
import { Logger } from '../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";

// Constants for the tool
const toolName = 'search_console_logs';
const toolDescription = 'Search Unity console logs with keyword or regex pattern. Supports case-sensitive/insensitive search, optional stack trace inclusion, and pagination.';

const paramsSchema = z.object({
  keyword: z.string().optional().describe('Keyword to search for (partial match)'),
  regex: z.string().optional().describe('Regular expression pattern (overrides keyword if provided)'),
  logType: z.enum(['error', 'warning', 'info']).optional().describe('Filter by log type (optional)'),
  includeStackTrace: z.boolean().optional().describe('Whether to include stack trace in search (default: true)'),
  caseSensitive: z.boolean().optional().describe('Whether the search is case sensitive (default: false)'),
  offset: z.number().int().min(0).optional().describe('Starting index for pagination (0-based, defaults to 0)'),
  limit: z.number().int().min(1).max(1000).optional().describe('Maximum number of logs to return (defaults to 50, max 1000)')
});

/**
 * Registers the search_console_logs tool with the MCP server
 */
export function registerSearchConsoleLogsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);
  
  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: z.infer<typeof paramsSchema>) => {
      logger.debug(`Executing tool ${toolName} with params:`, params);
      
      // Validate that at least one search criteria is provided
      if (!params.keyword && !params.regex) {
        throw new McpUnityError(
          ErrorType.VALIDATION, 
          'Either keyword or regex parameter must be provided'
        );
      }
      
      try {
        // Send the tool execution request to Unity
        const response = await mcpUnity.sendRequest({
          method: toolName,
          params: params
        });
        
        logger.debug(`Tool ${toolName} response:`, response);
        
        return {
          content: [
            {
              type: 'text' as const,
              text: JSON.stringify(response, null, 2)
            }
          ]
        } as CallToolResult;
      } catch (error) {
        logger.error(`Tool ${toolName} execution failed:`, error);
        
        if (error instanceof McpUnityError) {
          throw error;
        }
        
        throw new McpUnityError(
          ErrorType.TOOL_EXECUTION,
          `Failed to search console logs: ${(error as Error).message || String(error)}`
        );
      }
    }
  );
}