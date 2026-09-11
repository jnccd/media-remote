import axios from "axios";
import { getAuthHeaderForMediaControlAPI } from "./useEncryption";
import { serverOrigin } from "./serverAddress";

// The page origin is only the server when the page was *served by* the server.
// In the Tauri wrapper the webview serves the page itself, so `window.location`
// is the asset origin and relative URLs never reach the server. serverAddress.ts
// picks the right base for each context.
export const baseUrl = serverOrigin;
export const axiosClient = axios.create({
  baseURL: baseUrl,
  withCredentials: false,
});

var axiosClientPassword: string | null = null;
export const setAxiosClientPassword = (password: string | null) =>
  (axiosClientPassword = password);

export const postReqTo = async (route: string) => {
  const auth = await getAuthHeaderForMediaControlAPI(axiosClientPassword ?? "a");
  return axiosClient.post(`${route}`, {
    headers: {
      "Content-Type": "application/json",
      Authorization: auth,
    },
  });
};
