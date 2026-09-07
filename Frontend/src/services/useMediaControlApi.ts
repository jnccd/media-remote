import axios from "axios";
import { getAuthHeaderForMediaControlAPI } from "./useEncryption";

// In dev the frontend runs on a different origin (Vite dev server) than the backend, so we
// allow a VITE_DEV_BACKEND_ADDRESS override. In a production build (e.g. when the server
// serves its own frontend) we always use the same origin the page was loaded from.
export const baseUrl =
  import.meta.env.DEV && import.meta.env.VITE_DEV_BACKEND_ADDRESS
    ? import.meta.env.VITE_DEV_BACKEND_ADDRESS
    : window.location.href;
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
